import { useQuery } from '@tanstack/react-query'
import { useEffect, useMemo, useState } from 'react'
import { api, type ComparisonStatus } from '../api'
import { formatDate, formatMetric, formatPercent, metricLabel } from '../format'
import ScenarioNav from './ScenarioNav'

const statusLabels: Record<ComparisonStatus, string> = {
  Neutral: 'Sabit',
  Regression: 'Kötüleşme',
  Improvement: 'İyileşme',
  OnlyInBaseline: 'Sadece referansta',
  OnlyInTarget: 'Sadece hedefte',
}

function outcome(success: boolean | null) {
  if (success === null) return '—'
  return success ? 'başarılı' : 'başarısız'
}

export default function ScenarioComparePage() {
  const [suite, setSuite] = useState('')
  const [baselineRunId, setBaselineRunId] = useState('')
  const [targetRunId, setTargetRunId] = useState('')
  const [thresholdPercent, setThresholdPercent] = useState(10)
  const [onlyChanges, setOnlyChanges] = useState(true)

  const suites = useQuery({ queryKey: ['scenario-suites'], queryFn: api.scenarioSuites })
  const runs = useQuery({
    queryKey: ['scenario-runs', suite],
    queryFn: () => api.scenarioRuns(suite),
    enabled: Boolean(suite),
  })

  useEffect(() => {
    if (!suite && suites.data?.length) setSuite(suites.data[0].suite)
  }, [suites.data, suite])

  // Comparisons only make sense inside a suite; picking a new suite resets the runs.
  useEffect(() => {
    const items = runs.data?.items ?? []
    const ids = new Set(items.map((run) => run.id))
    if (items.length >= 2 && (!ids.has(baselineRunId) || !ids.has(targetRunId))) {
      setTargetRunId(items[0].id)
      setBaselineRunId(items[1].id)
    }
  }, [runs.data, baselineRunId, targetRunId])

  const comparison = useQuery({
    queryKey: ['scenario-compare', baselineRunId, targetRunId, thresholdPercent],
    queryFn: () => api.scenarioCompare(baselineRunId, targetRunId, thresholdPercent / 100),
    enabled: Boolean(baselineRunId && targetRunId && baselineRunId !== targetRunId),
  })

  const rows = useMemo(
    () =>
      // Run-level rows always stay. For cases, "changes" means a judged movement; a case
      // missing from one run is already listed in the outcome table above.
      (comparison.data?.rows ?? []).filter(
        (row) => !onlyChanges || row.case === null || row.status === 'Regression' || row.status === 'Improvement',
      ),
    [comparison.data, onlyChanges],
  )

  const runOptions = runs.data?.items ?? []

  return (
    <>
      <h1>Senaryo karşılaştır</h1>
      <p className="subtitle">
        Aynı setin iki koşusunu metrik ve vaka bazında karşılaştırır. Kötüleşme/iyileşme yalnızca yönü bilinen
        metriklerde işaretlenir.
      </p>
      <ScenarioNav />

      <div className="toolbar">
        <select value={suite} onChange={(event) => setSuite(event.target.value)}>
          {suites.data?.map((item) => (
            <option key={item.suite} value={item.suite}>
              {item.suite}
            </option>
          ))}
        </select>
        <label className="meta">
          Referans{' '}
          <select value={baselineRunId} onChange={(event) => setBaselineRunId(event.target.value)}>
            {runOptions.map((run) => (
              <option key={run.id} value={run.id}>
                {run.label ?? formatDate(run.startedAt)} — {formatDate(run.startedAt)}
              </option>
            ))}
          </select>
        </label>
        <label className="meta">
          Hedef{' '}
          <select value={targetRunId} onChange={(event) => setTargetRunId(event.target.value)}>
            {runOptions.map((run) => (
              <option key={run.id} value={run.id}>
                {run.label ?? formatDate(run.startedAt)} — {formatDate(run.startedAt)}
              </option>
            ))}
          </select>
        </label>
        <label className="meta">
          Eşik %{' '}
          <input
            type="number"
            min={1}
            max={100}
            value={thresholdPercent}
            onChange={(event) => setThresholdPercent(Number(event.target.value) || 1)}
            style={{ width: '5rem' }}
          />
        </label>
        <label className="meta">
          <input type="checkbox" checked={onlyChanges} onChange={(event) => setOnlyChanges(event.target.checked)} />{' '}
          Yalnızca değişen vaka metrikleri
        </label>
      </div>

      {suite && runOptions.length < 2 && runs.isSuccess && (
        <div className="state">Karşılaştırma için bu sette en az iki koşu gerekir.</div>
      )}
      {comparison.isError && <div className="state error">{(comparison.error as Error).message}</div>}

      {comparison.data && (
        <>
          <div className="stat-row">
            <div className="card">
              <div className="stat-label">Kötüleşme</div>
              <div className="stat-value" style={{ color: comparison.data.regressionCount ? 'var(--bad)' : undefined }}>
                {comparison.data.regressionCount}
              </div>
            </div>
            <div className="card">
              <div className="stat-label">İyileşme</div>
              <div className="stat-value" style={{ color: comparison.data.improvementCount ? 'var(--good)' : undefined }}>
                {comparison.data.improvementCount}
              </div>
            </div>
            <div className="card">
              <div className="stat-label">Sonucu değişen vaka</div>
              <div className="stat-value">{comparison.data.outcomeChanges.length}</div>
            </div>
          </div>

          {comparison.data.outcomeChanges.length > 0 && (
            <div className="table-wrap" style={{ marginBottom: '1rem' }}>
              <table>
                <thead>
                  <tr>
                    <th>Sonucu değişen vaka</th>
                    <th>Referans</th>
                    <th>Hedef</th>
                  </tr>
                </thead>
                <tbody>
                  {comparison.data.outcomeChanges.map((change) => (
                    <tr key={change.case}>
                      <td>{change.case}</td>
                      <td className="num">{outcome(change.baselineSuccess)}</td>
                      <td className="num">
                        <span
                          className={
                            change.targetSuccess === false ? 'delta-bad' : change.targetSuccess ? 'delta-good' : undefined
                          }
                        >
                          {outcome(change.targetSuccess)}
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Kapsam</th>
                  <th>Metrik</th>
                  <th>Referans</th>
                  <th>Hedef</th>
                  <th>Fark</th>
                  <th>Durum</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => (
                  <tr key={`${row.case ?? ''}|${row.metric}`}>
                    <td>{row.case ?? <strong>Koşu geneli</strong>}</td>
                    <td title={row.metric}>{metricLabel(row.metric)}</td>
                    <td className="num">{formatMetric(row.baselineValue, row.unit)}</td>
                    <td className="num">{formatMetric(row.targetValue, row.unit)}</td>
                    <td className="num">
                      <span
                        className={
                          row.status === 'Regression' ? 'delta-bad' : row.status === 'Improvement' ? 'delta-good' : undefined
                        }
                      >
                        {formatPercent(row.deltaPercent)}
                      </span>
                    </td>
                    <td className="num">
                      <span
                        className={
                          row.status === 'Regression'
                            ? 'badge bad'
                            : row.status === 'Improvement'
                              ? 'badge good'
                              : 'badge'
                        }
                      >
                        {row.direction === 'none' && row.status === 'Neutral' ? 'Yönsüz' : statusLabels[row.status]}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}
    </>
  )
}

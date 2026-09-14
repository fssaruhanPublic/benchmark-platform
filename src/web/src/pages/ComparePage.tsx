import { useQuery } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { api, type ComparisonStatus } from '../api'
import { formatBytes, formatDate, formatDuration, formatPercent } from '../format'

const statusLabels: Record<ComparisonStatus, string> = {
  Neutral: 'Sabit',
  Regression: 'Regresyon',
  Improvement: 'İyileşme',
  OnlyInBaseline: 'Sadece referansta',
  OnlyInTarget: 'Sadece hedefte',
}

export default function ComparePage() {
  const [baselineRunId, setBaselineRunId] = useState('')
  const [targetRunId, setTargetRunId] = useState('')
  const [thresholdPercent, setThresholdPercent] = useState(10)

  const runs = useQuery({ queryKey: ['runs', ''], queryFn: () => api.runs() })

  useEffect(() => {
    const items = runs.data?.items ?? []
    if (items.length >= 2 && !baselineRunId && !targetRunId) {
      setTargetRunId(items[0].id)
      setBaselineRunId(items[1].id)
    }
  }, [runs.data, baselineRunId, targetRunId])

  const comparison = useQuery({
    queryKey: ['compare', baselineRunId, targetRunId, thresholdPercent],
    queryFn: () => api.compare(baselineRunId, targetRunId, thresholdPercent / 100),
    enabled: Boolean(baselineRunId && targetRunId),
  })

  const runOptions = runs.data?.items ?? []

  return (
    <>
      <h1>Karşılaştır</h1>
      <p className="subtitle">İki koşuyu benchmark bazında karşılaştırır ve eşiği aşan farkları işaretler.</p>

      <div className="toolbar">
        <label className="meta">
          Referans{' '}
          <select value={baselineRunId} onChange={(event) => setBaselineRunId(event.target.value)}>
            <option value="">Seçin…</option>
            {runOptions.map((run) => (
              <option key={run.id} value={run.id}>
                {run.label ?? formatDate(run.runAt)} — {formatDate(run.runAt)}
              </option>
            ))}
          </select>
        </label>
        <label className="meta">
          Hedef{' '}
          <select value={targetRunId} onChange={(event) => setTargetRunId(event.target.value)}>
            <option value="">Seçin…</option>
            {runOptions.map((run) => (
              <option key={run.id} value={run.id}>
                {run.label ?? formatDate(run.runAt)} — {formatDate(run.runAt)}
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
      </div>

      {runOptions.length < 2 && (
        <div className="state">Karşılaştırma için en az iki koşu gerekir.</div>
      )}
      {comparison.isError && <div className="state error">{(comparison.error as Error).message}</div>}

      {comparison.data && (
        <>
          <div className="stat-row">
            <div className="card">
              <div className="stat-label">Regresyon</div>
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
              <div className="stat-label">Karşılaştırılan</div>
              <div className="stat-value">{comparison.data.rows.length}</div>
            </div>
          </div>

          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Benchmark</th>
                  <th>Referans</th>
                  <th>Hedef</th>
                  <th>Fark</th>
                  <th>Ref. bellek</th>
                  <th>Hedef bellek</th>
                  <th>Bellek farkı</th>
                  <th>Durum</th>
                </tr>
              </thead>
              <tbody>
                {comparison.data.rows.map((row) => (
                  <tr key={row.fullName}>
                    <td>{row.fullName}</td>
                    <td className="num">{row.baselineMeanNs === null ? '—' : formatDuration(row.baselineMeanNs)}</td>
                    <td className="num">{row.targetMeanNs === null ? '—' : formatDuration(row.targetMeanNs)}</td>
                    <td className="num">
                      <span
                        className={
                          row.meanDeltaPercent === null
                            ? undefined
                            : row.meanDeltaPercent > 0
                              ? 'delta-bad'
                              : 'delta-good'
                        }
                      >
                        {formatPercent(row.meanDeltaPercent)}
                      </span>
                    </td>
                    <td className="num">{formatBytes(row.baselineAllocatedBytes)}</td>
                    <td className="num">{formatBytes(row.targetAllocatedBytes)}</td>
                    <td className="num">{formatPercent(row.allocatedDeltaPercent)}</td>
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
                        {statusLabels[row.status]}
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

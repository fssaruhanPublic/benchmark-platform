import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { api, type ScenarioRunSummary } from '../api'
import { formatDate, formatMetric, formatNumber, metricLabel } from '../format'
import ScenarioNav from './ScenarioNav'

/** The metrics shown on a run card, in this order when present. */
const headlineMetrics = ['accuracy', 'consistency', 'pass.rate', 'latency.p95', 'error.rate', 'throughput', 'cost.total']

function headline(run: ScenarioRunSummary) {
  const preferred = headlineMetrics
    .map((name) => run.metrics.find((metric) => metric.name === name))
    .filter((metric) => metric !== undefined)
  return (preferred.length > 0 ? preferred : run.metrics).slice(0, 4)
}

export default function ScenariosPage() {
  const [suite, setSuite] = useState('')
  const queryClient = useQueryClient()

  const suites = useQuery({ queryKey: ['scenario-suites'], queryFn: api.scenarioSuites })
  const runs = useQuery({ queryKey: ['scenario-runs', suite], queryFn: () => api.scenarioRuns(suite) })

  const remove = useMutation({
    mutationFn: api.deleteScenarioRun,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['scenario-runs'] })
      void queryClient.invalidateQueries({ queryKey: ['scenario-suites'] })
    },
  })

  return (
    <>
      <h1>Senaryolar</h1>
      <p className="subtitle">
        Değerlendirme setleri ve yük testleri. Her koşu bir <code>scenario-run/v1</code> belgesi olarak{' '}
        <code>POST /api/scenario-runs</code> ile gelir.
      </p>
      <ScenarioNav />

      <div className="stat-row">
        {suites.data?.map((item) => (
          <button
            key={item.suite}
            className={`card suite-card${suite === item.suite ? ' selected' : ''}`}
            onClick={() => setSuite(suite === item.suite ? '' : item.suite)}
          >
            <div className="stat-label">
              {item.kind} · {item.runCount} koşu
            </div>
            <div className="suite-name">{item.suite}</div>
            <div className="meta">Son: {formatDate(item.lastRunAt)}</div>
          </button>
        ))}
      </div>

      {runs.isPending && <div className="state">Yükleniyor…</div>}
      {runs.isError && <div className="state error">{(runs.error as Error).message}</div>}
      {runs.data?.items.length === 0 && (
        <div className="state">
          Henüz senaryo koşusu yok. Grafirio tarafında değerlendirme ya da yük testi aracını <code>--publish</code>{' '}
          ile çalıştırın.
        </div>
      )}

      <div className="run-list">
        {runs.data?.items.map((run) => {
          const passRate = run.caseCount > 0 ? run.passedCount / run.caseCount : null
          return (
            <div className="card run-card" key={run.id}>
              <div>
                <div className="run-title">
                  <Link to={`/scenarios/${run.id}`}>{run.label ?? formatDate(run.startedAt)}</Link>{' '}
                  <span className="badge">{run.kind}</span>{' '}
                  <span className="badge">{run.suite}</span>{' '}
                  {passRate !== null && (
                    <span className={`badge ${passRate === 1 ? 'good' : passRate < 0.8 ? 'bad' : ''}`}>
                      {run.passedCount}/{run.caseCount} başarılı
                    </span>
                  )}
                </div>
                <div className="meta">
                  {formatDate(run.startedAt)} · {run.machineName || '—'}
                  {run.target ? ` · ${run.target}` : ''}
                  {run.gitCommit ? ` · ${run.gitBranch ?? ''}@${run.gitCommit}` : ''} ·{' '}
                  {formatNumber(run.durationMs / 1000)} sn
                </div>
                <div className="metric-chips">
                  {headline(run).map((metric) => (
                    <span key={metric.name} className="metric-chip">
                      <span className="meta">{metricLabel(metric.name)}</span>{' '}
                      <strong>{formatMetric(metric.value, metric.unit)}</strong>
                    </span>
                  ))}
                </div>
              </div>
              <button
                onClick={() => {
                  if (confirm('Bu senaryo koşusu ve tüm sonuçları silinsin mi?')) {
                    remove.mutate(run.id)
                  }
                }}
              >
                Sil
              </button>
            </div>
          )
        })}
      </div>
    </>
  )
}

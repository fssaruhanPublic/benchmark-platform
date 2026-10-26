import { useQuery } from '@tanstack/react-query'
import { Fragment, useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api, type ScenarioCase } from '../api'
import { formatDate, formatMetric, formatNumber, metricLabel } from '../format'
import ScenarioNav from './ScenarioNav'

type Filter = 'all' | 'failed' | 'passed'

export default function ScenarioRunPage() {
  const { id = '' } = useParams()
  const [filter, setFilter] = useState<Filter>('all')
  const [search, setSearch] = useState('')
  const [expanded, setExpanded] = useState<string | null>(null)

  const detail = useQuery({ queryKey: ['scenario-run', id], queryFn: () => api.scenarioRun(id), enabled: Boolean(id) })

  // Case metrics become columns; their unit is taken from the first case that has them.
  const columns = useMemo(() => {
    const units = new Map<string, string>()
    for (const item of detail.data?.cases ?? []) {
      for (const metric of item.metrics) {
        if (!units.has(metric.name)) units.set(metric.name, metric.unit)
      }
    }
    return [...units.entries()].map(([name, unit]) => ({ name, unit }))
  }, [detail.data])

  const cases = useMemo(() => {
    const term = search.trim().toLowerCase()
    return (detail.data?.cases ?? []).filter(
      (item: ScenarioCase) =>
        (filter === 'all' || (filter === 'failed' ? !item.success : item.success)) &&
        (!term || item.name.toLowerCase().includes(term) || item.group.toLowerCase().includes(term)),
    )
  }, [detail.data, filter, search])

  if (detail.isPending) return <div className="state">Yükleniyor…</div>
  if (detail.isError) return <div className="state error">{(detail.error as Error).message}</div>
  if (!detail.data) return <div className="state">Koşu bulunamadı.</div>

  const { run, environment } = detail.data
  const failed = run.caseCount - run.passedCount

  return (
    <>
      <h1>{run.label ?? formatDate(run.startedAt)}</h1>
      <p className="subtitle">
        <Link to="/scenarios">← Senaryolar</Link> · {run.kind} · {run.suite} · {formatDate(run.startedAt)} ·{' '}
        {formatNumber(run.durationMs / 1000)} sn{run.target ? ` · ${run.target}` : ''}
        {run.gitCommit ? ` · ${run.gitBranch ?? ''}@${run.gitCommit}` : ''}
      </p>
      <ScenarioNav />

      <div className="stat-row">
        <div className="card">
          <div className="stat-label">Başarılı vaka</div>
          <div className="stat-value">
            {run.passedCount}/{run.caseCount}
          </div>
        </div>
        {run.metrics.map((metric) => (
          <div className="card" key={metric.name}>
            <div className="stat-label" title={metric.name}>
              {metricLabel(metric.name)}
            </div>
            <div className="stat-value">{formatMetric(metric.value, metric.unit)}</div>
          </div>
        ))}
      </div>

      {Object.keys(environment).length > 0 && (
        <div className="card env-card">
          <h2>Ortam</h2>
          <dl className="env-list">
            {Object.entries(environment).map(([key, value]) => (
              <Fragment key={key}>
                <dt>{key}</dt>
                <dd>{value}</dd>
              </Fragment>
            ))}
          </dl>
        </div>
      )}

      <div className="toolbar">
        <select value={filter} onChange={(event) => setFilter(event.target.value as Filter)}>
          <option value="all">Tüm vakalar ({run.caseCount})</option>
          <option value="failed">Başarısız ({failed})</option>
          <option value="passed">Başarılı ({run.passedCount})</option>
        </select>
        <input
          type="search"
          placeholder="Vaka ya da grup ara"
          value={search}
          onChange={(event) => setSearch(event.target.value)}
          style={{ flex: '1 1 260px' }}
        />
        <span className="meta">{cases.length} satır</span>
      </div>

      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Vaka</th>
              <th>Grup</th>
              <th>Sonuç</th>
              {columns.map((column) => (
                <th key={column.name} title={column.name}>
                  {metricLabel(column.name)}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {cases.map((item) => (
              <Fragment key={item.id}>
                <tr
                  className={item.details || item.message ? 'clickable' : undefined}
                  onClick={() => setExpanded(expanded === item.id ? null : item.id)}
                >
                  <td>{item.name}</td>
                  <td className="meta">{item.group || '—'}</td>
                  <td>
                    <span className={`badge ${item.success ? 'good' : 'bad'}`}>
                      {item.success ? 'başarılı' : 'başarısız'}
                    </span>
                  </td>
                  {columns.map((column) => {
                    const metric = item.metrics.find((m) => m.name === column.name)
                    return (
                      <td key={column.name} className="num">
                        {metric ? formatMetric(metric.value, metric.unit) : '—'}
                      </td>
                    )
                  })}
                </tr>
                {expanded === item.id && (item.details != null || item.message) && (
                  <tr className="details-row">
                    <td colSpan={3 + columns.length}>
                      {item.message && <p className="case-message">{item.message}</p>}
                      {item.details != null && <pre>{JSON.stringify(item.details, null, 2)}</pre>}
                    </td>
                  </tr>
                )}
              </Fragment>
            ))}
          </tbody>
        </table>
      </div>
    </>
  )
}

import { useQuery } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { api, type BenchmarkResult } from '../api'
import { formatBytes, formatDate, formatDuration, formatNumber, shortName } from '../format'

type SortKey = keyof Pick<
  BenchmarkResult,
  'fullName' | 'meanNs' | 'errorNs' | 'stdDevNs' | 'medianNs' | 'p95Ns' | 'opsPerSecond' | 'allocatedBytes' | 'gen0'
>

const columns: { key: SortKey; label: string; render: (r: BenchmarkResult) => string }[] = [
  { key: 'fullName', label: 'Benchmark', render: (r) => r.fullName },
  { key: 'meanNs', label: 'Ortalama', render: (r) => formatDuration(r.meanNs) },
  { key: 'errorNs', label: 'Hata', render: (r) => formatDuration(r.errorNs) },
  { key: 'stdDevNs', label: 'Std sapma', render: (r) => formatDuration(r.stdDevNs) },
  { key: 'medianNs', label: 'Medyan', render: (r) => formatDuration(r.medianNs) },
  { key: 'p95Ns', label: 'P95', render: (r) => formatDuration(r.p95Ns) },
  { key: 'opsPerSecond', label: 'Ops/sn', render: (r) => formatNumber(r.opsPerSecond) },
  { key: 'gen0', label: 'Gen0/1k', render: (r) => formatNumber(r.gen0) },
  { key: 'allocatedBytes', label: 'Ayrılan', render: (r) => formatBytes(r.allocatedBytes) },
]

export default function RunDetailPage() {
  const { id = '' } = useParams()
  const [sortKey, setSortKey] = useState<SortKey>('meanNs')
  const [ascending, setAscending] = useState(true)
  const [filter, setFilter] = useState('')

  const detail = useQuery({ queryKey: ['run', id], queryFn: () => api.run(id), enabled: Boolean(id) })

  const rows = useMemo(() => {
    const all = detail.data?.results ?? []
    const term = filter.trim().toLowerCase()
    const filtered = term ? all.filter((r) => r.fullName.toLowerCase().includes(term)) : all

    return [...filtered].sort((a, b) => {
      const left = a[sortKey]
      const right = b[sortKey]
      const compared =
        typeof left === 'string' && typeof right === 'string' ? left.localeCompare(right) : Number(left) - Number(right)
      return ascending ? compared : -compared
    })
  }, [detail.data, sortKey, ascending, filter])

  const chartData = useMemo(
    () => rows.filter((r) => r.success).map((r) => ({ name: shortName(r.fullName), mean: r.meanNs })),
    [rows],
  )

  if (detail.isPending) return <div className="state">Yükleniyor…</div>
  if (detail.isError) return <div className="state error">{(detail.error as Error).message}</div>
  if (!detail.data) return <div className="state">Koşu bulunamadı.</div>

  const { run } = detail.data

  return (
    <>
      <h1>{run.label ?? formatDate(run.runAt)}</h1>
      <p className="subtitle">
        <Link to="/runs">← Koşular</Link> · {formatDate(run.runAt)} · {run.machineName} · {run.runtimeVersion} ·{' '}
        {run.buildConfiguration}
        {run.gitCommit ? ` · ${run.gitBranch ?? ''}@${run.gitCommit}` : ''}
      </p>

      <div className="toolbar">
        <input
          type="search"
          placeholder="Benchmark filtrele"
          value={filter}
          onChange={(event) => setFilter(event.target.value)}
          style={{ flex: '1 1 260px' }}
        />
        <span className="meta">{rows.length} satır</span>
      </div>

      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              {columns.map((column) => (
                <th
                  key={column.key}
                  onClick={() => {
                    if (sortKey === column.key) {
                      setAscending((value) => !value)
                    } else {
                      setSortKey(column.key)
                      setAscending(true)
                    }
                  }}
                >
                  {column.label}
                  {sortKey === column.key ? (ascending ? ' ▲' : ' ▼') : ''}
                </th>
              ))}
              <th>Oran</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((result) => (
              <tr key={result.id}>
                {columns.map((column) => (
                  <td key={column.key} className={column.key === 'fullName' ? undefined : 'num'}>
                    {column.key === 'fullName' && result.isBaseline ? (
                      <>
                        {column.render(result)} <span className="badge">baseline</span>
                      </>
                    ) : (
                      column.render(result)
                    )}
                  </td>
                ))}
                <td className="num">
                  {result.ratioToBaseline === null ? (
                    '—'
                  ) : (
                    <span className={result.ratioToBaseline > 1 ? 'delta-bad' : 'delta-good'}>
                      {formatNumber(result.ratioToBaseline)}×
                    </span>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="card chart-card">
        <h2>Ortalama süre</h2>
        <ResponsiveContainer width="100%" height={Math.max(240, chartData.length * 26)}>
          <BarChart data={chartData} layout="vertical" margin={{ left: 12, right: 24 }}>
            <CartesianGrid stroke="var(--grid)" horizontal={false} />
            <XAxis type="number" stroke="var(--text-muted)" fontSize={12} tickFormatter={(v) => formatDuration(v)} />
            <YAxis type="category" dataKey="name" width={230} stroke="var(--text-muted)" fontSize={12} />
            <Tooltip
              formatter={(value) => formatDuration(Number(value))}
              contentStyle={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 8 }}
            />
            <Bar dataKey="mean" fill="var(--accent)" radius={[0, 4, 4, 0]} />
          </BarChart>
        </ResponsiveContainer>
      </div>
    </>
  )
}

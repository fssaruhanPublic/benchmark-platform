import { useQuery } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import {
  CartesianGrid,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import { api } from '../api'
import { formatBytes, formatDate, formatDuration } from '../format'

export default function TrendsPage() {
  const [selected, setSelected] = useState('')

  const benchmarks = useQuery({ queryKey: ['benchmarks'], queryFn: api.benchmarks })

  useEffect(() => {
    if (!selected && benchmarks.data?.length) {
      setSelected(benchmarks.data[0].fullName)
    }
  }, [benchmarks.data, selected])

  const history = useQuery({
    queryKey: ['history', selected],
    queryFn: () => api.history(selected),
    enabled: Boolean(selected),
  })

  const points =
    history.data?.points.map((point) => ({
      ...point,
      x: formatDate(point.runAt),
      allocatedKb: point.allocatedBytes / 1024,
    })) ?? []

  return (
    <>
      <h1>Trendler</h1>
      <p className="subtitle">Bir benchmark'ın koşular boyunca nasıl değiştiğini gösterir.</p>

      <div className="toolbar">
        <select value={selected} onChange={(event) => setSelected(event.target.value)} style={{ flex: '1 1 340px' }}>
          {benchmarks.data?.map((benchmark) => (
            <option key={benchmark.fullName} value={benchmark.fullName}>
              {benchmark.fullName} ({benchmark.runCount} koşu)
            </option>
          ))}
        </select>
      </div>

      {benchmarks.isPending && <div className="state">Yükleniyor…</div>}
      {benchmarks.data?.length === 0 && <div className="state">Henüz ölçüm kaydı yok.</div>}
      {history.isError && <div className="state error">{(history.error as Error).message}</div>}

      {points.length > 0 && (
        <div className="grid-2">
          <div className="card">
            <h2>Ortalama süre</h2>
            <ResponsiveContainer width="100%" height={300}>
              <LineChart data={points} margin={{ left: 8, right: 16 }}>
                <CartesianGrid stroke="var(--grid)" />
                <XAxis dataKey="x" stroke="var(--text-muted)" fontSize={11} minTickGap={24} />
                <YAxis stroke="var(--text-muted)" fontSize={11} tickFormatter={(v) => formatDuration(v)} width={80} />
                <Tooltip
                  formatter={(value) => formatDuration(Number(value))}
                  contentStyle={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 8 }}
                />
                <Line type="monotone" dataKey="meanNs" stroke="var(--accent)" strokeWidth={2} dot={{ r: 3 }} />
              </LineChart>
            </ResponsiveContainer>
          </div>

          <div className="card">
            <h2>Ayrılan bellek</h2>
            <ResponsiveContainer width="100%" height={300}>
              <LineChart data={points} margin={{ left: 8, right: 16 }}>
                <CartesianGrid stroke="var(--grid)" />
                <XAxis dataKey="x" stroke="var(--text-muted)" fontSize={11} minTickGap={24} />
                <YAxis stroke="var(--text-muted)" fontSize={11} width={70} unit=" KB" />
                <Tooltip
                  formatter={(value) => formatBytes(Number(value) * 1024)}
                  contentStyle={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 8 }}
                />
                <Line type="monotone" dataKey="allocatedKb" stroke="var(--good)" strokeWidth={2} dot={{ r: 3 }} />
              </LineChart>
            </ResponsiveContainer>
          </div>
        </div>
      )}

      {selected && points.length === 1 && (
        <p className="subtitle" style={{ marginTop: '1rem' }}>
          Trend çizgisi için en az iki koşu gerekir — benchmark'ı bir kez daha çalıştırın.
        </p>
      )}
    </>
  )
}

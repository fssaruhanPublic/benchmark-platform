import { useQuery } from '@tanstack/react-query'
import { useEffect, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { CartesianGrid, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { api } from '../api'
import { formatDate, formatMetric, metricLabel } from '../format'
import ScenarioNav from './ScenarioNav'

export default function ScenarioTrendsPage() {
  const navigate = useNavigate()
  const [suite, setSuite] = useState('')
  const [metric, setMetric] = useState('')
  const [caseName, setCaseName] = useState('')

  const suites = useQuery({ queryKey: ['scenario-suites'], queryFn: api.scenarioSuites })
  const current = suites.data?.find((item) => item.suite === suite)

  useEffect(() => {
    if (!suite && suites.data?.length) setSuite(suites.data[0].suite)
  }, [suites.data, suite])

  // A case-level metric needs a case; a run-level one must not have one.
  const isCaseMetric = Boolean(current?.caseMetrics.includes(metric) && !current?.runMetrics.includes(metric))

  useEffect(() => {
    if (!current) return
    if (!metric || (!current.runMetrics.includes(metric) && !current.caseMetrics.includes(metric))) {
      setMetric(current.runMetrics[0] ?? current.caseMetrics[0] ?? '')
    }
  }, [current, metric])

  useEffect(() => {
    if (isCaseMetric && !caseName && current?.cases.length) setCaseName(current.cases[0])
    if (!isCaseMetric && caseName) setCaseName('')
  }, [isCaseMetric, caseName, current])

  const history = useQuery({
    queryKey: ['scenario-history', suite, metric, caseName],
    queryFn: () => api.scenarioHistory(suite, metric, caseName || undefined),
    enabled: Boolean(suite && metric && (!isCaseMetric || caseName)),
  })

  const points = useMemo(
    () =>
      history.data?.points.map((point) => ({
        ...point,
        x: `${formatDate(point.startedAt)}${point.gitCommit ? ` @${point.gitCommit}` : ''}`,
      })) ?? [],
    [history.data],
  )

  const unit = history.data?.unit ?? ''
  const direction = history.data?.direction

  return (
    <>
      <h1>Senaryo trendleri</h1>
      <p className="subtitle">Bir metriğin koşular boyunca nasıl değiştiğini gösterir. Bir noktaya tıklayınca o koşu açılır.</p>
      <ScenarioNav />

      <div className="toolbar">
        <select value={suite} onChange={(event) => setSuite(event.target.value)}>
          {suites.data?.map((item) => (
            <option key={item.suite} value={item.suite}>
              {item.suite} ({item.runCount} koşu)
            </option>
          ))}
        </select>
        <select value={metric} onChange={(event) => setMetric(event.target.value)}>
          {current && current.runMetrics.length > 0 && (
            <optgroup label="Koşu geneli">
              {current.runMetrics.map((name) => (
                <option key={`run-${name}`} value={name}>
                  {metricLabel(name)}
                </option>
              ))}
            </optgroup>
          )}
          {current && current.caseMetrics.some((name) => !current.runMetrics.includes(name)) && (
            <optgroup label="Vaka bazında">
              {current.caseMetrics
                .filter((name) => !current.runMetrics.includes(name))
                .map((name) => (
                  <option key={`case-${name}`} value={name}>
                    {metricLabel(name)}
                  </option>
                ))}
            </optgroup>
          )}
        </select>
        {isCaseMetric && (
          <select value={caseName} onChange={(event) => setCaseName(event.target.value)} style={{ flex: '1 1 260px' }}>
            {current?.cases.map((name) => (
              <option key={name} value={name}>
                {name}
              </option>
            ))}
          </select>
        )}
        {direction && direction !== 'none' && (
          <span className="meta">{direction === 'higher' ? 'Yüksek olması iyi' : 'Düşük olması iyi'}</span>
        )}
      </div>

      {suites.data?.length === 0 && <div className="state">Henüz senaryo koşusu yok.</div>}
      {history.isError && <div className="state error">{(history.error as Error).message}</div>}

      {points.length > 0 && (
        <div className="card">
          <h2>
            {metricLabel(metric)}
            {caseName ? ` — ${caseName}` : ''}
          </h2>
          <ResponsiveContainer width="100%" height={340}>
            <LineChart
              data={points}
              margin={{ left: 8, right: 16 }}
              onClick={(state) => {
                const index = Number(state?.activeTooltipIndex)
                if (Number.isInteger(index) && points[index]) navigate(`/scenarios/${points[index].runId}`)
              }}
            >
              <CartesianGrid stroke="var(--grid)" />
              <XAxis dataKey="x" stroke="var(--text-muted)" fontSize={11} minTickGap={24} />
              <YAxis
                stroke="var(--text-muted)"
                fontSize={11}
                width={90}
                tickFormatter={(value) => formatMetric(Number(value), unit)}
                domain={unit === 'ratio' ? [0, 1] : ['auto', 'auto']}
              />
              <Tooltip
                formatter={(value) => formatMetric(Number(value), unit)}
                labelFormatter={(label, payload) => {
                  const point = payload?.[0]?.payload as { label?: string | null; success?: boolean | null } | undefined
                  const parts = [String(label)]
                  if (point?.label) parts.push(point.label)
                  if (point?.success === false) parts.push('vaka başarısız')
                  return parts.join(' · ')
                }}
                contentStyle={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 8 }}
              />
              <Line type="monotone" dataKey="value" stroke="var(--accent)" strokeWidth={2} dot={{ r: 3 }} />
            </LineChart>
          </ResponsiveContainer>
        </div>
      )}

      {points.length === 1 && (
        <p className="subtitle" style={{ marginTop: '1rem' }}>
          Trend çizgisi için en az iki koşu gerekir.
        </p>
      )}
    </>
  )
}

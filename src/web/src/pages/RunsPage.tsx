import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api'
import { formatDate, formatNumber } from '../format'

export default function RunsPage() {
  const [search, setSearch] = useState('')
  const queryClient = useQueryClient()

  const summary = useQuery({ queryKey: ['summary'], queryFn: api.summary })
  const runs = useQuery({ queryKey: ['runs', search], queryFn: () => api.runs(search) })

  const remove = useMutation({
    mutationFn: api.deleteRun,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['runs'] })
      void queryClient.invalidateQueries({ queryKey: ['summary'] })
    },
  })

  return (
    <>
      <h1>Koşular</h1>
      <p className="subtitle">
        Her BenchmarkDotNet çalıştırması buraya otomatik olarak düşer.
      </p>

      <div className="stat-row">
        <div className="card">
          <div className="stat-label">Toplam koşu</div>
          <div className="stat-value">{formatNumber(summary.data?.runCount ?? 0)}</div>
        </div>
        <div className="card">
          <div className="stat-label">Benchmark sayısı</div>
          <div className="stat-value">{formatNumber(summary.data?.benchmarkCount ?? 0)}</div>
        </div>
        <div className="card">
          <div className="stat-label">Ölçüm kaydı</div>
          <div className="stat-value">{formatNumber(summary.data?.resultCount ?? 0)}</div>
        </div>
        <div className="card">
          <div className="stat-label">Son koşuda regresyon</div>
          <div className="stat-value" style={{ color: summary.data?.regressionsVsPreviousRun ? 'var(--bad)' : undefined }}>
            {formatNumber(summary.data?.regressionsVsPreviousRun ?? 0)}
          </div>
        </div>
      </div>

      <div className="toolbar">
        <input
          type="search"
          placeholder="Etiket, makine veya commit ara"
          value={search}
          onChange={(event) => setSearch(event.target.value)}
          style={{ flex: '1 1 260px' }}
        />
        <button onClick={() => void runs.refetch()}>Yenile</button>
      </div>

      {runs.isPending && <div className="state">Yükleniyor…</div>}
      {runs.isError && <div className="state error">{(runs.error as Error).message}</div>}
      {runs.data?.items.length === 0 && (
        <div className="state">
          Henüz koşu yok. Şu komutu çalıştırın:
          <br />
          <code>dotnet run -c Release --project src/BenchmarkPlatform.Benchmarks -- --filter *</code>
        </div>
      )}

      <div className="run-list">
        {runs.data?.items.map((run) => (
          <div className="card run-card" key={run.id}>
            <div>
              <div className="run-title">
                <Link to={`/runs/${run.id}`}>{run.label ?? formatDate(run.runAt)}</Link>{' '}
                <span className="badge">{run.resultCount} benchmark</span>
              </div>
              <div className="meta">
                {formatDate(run.runAt)} · {run.machineName} · {run.runtimeVersion} · {run.buildConfiguration}
                {run.gitCommit ? ` · ${run.gitBranch ?? ''}@${run.gitCommit}` : ''}
              </div>
            </div>
            <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
              <span className="meta">{formatNumber(run.totalDurationMs / 1000)} sn</span>
              <button
                onClick={() => {
                  if (confirm('Bu koşu ve tüm sonuçları silinsin mi?')) {
                    remove.mutate(run.id)
                  }
                }}
              >
                Sil
              </button>
            </div>
          </div>
        ))}
      </div>
    </>
  )
}

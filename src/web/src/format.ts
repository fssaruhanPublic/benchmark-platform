const numberFormat = new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 2 })

/** Nanoseconds rendered with the unit BenchmarkDotNet would have picked. */
export function formatDuration(ns: number): string {
  if (!Number.isFinite(ns) || ns <= 0) return '—'
  if (ns < 1_000) return `${numberFormat.format(ns)} ns`
  if (ns < 1_000_000) return `${numberFormat.format(ns / 1_000)} µs`
  if (ns < 1_000_000_000) return `${numberFormat.format(ns / 1_000_000)} ms`
  return `${numberFormat.format(ns / 1_000_000_000)} s`
}

export function formatBytes(bytes: number | null | undefined): string {
  if (bytes === null || bytes === undefined) return '—'
  if (bytes === 0) return '0 B'
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${numberFormat.format(bytes / 1024)} KB`
  return `${numberFormat.format(bytes / (1024 * 1024))} MB`
}

export function formatNumber(value: number | null | undefined): string {
  if (value === null || value === undefined || !Number.isFinite(value)) return '—'
  return numberFormat.format(value)
}

export function formatPercent(ratio: number | null | undefined): string {
  if (ratio === null || ratio === undefined || !Number.isFinite(ratio)) return '—'
  const sign = ratio > 0 ? '+' : ''
  return `${sign}${numberFormat.format(ratio * 100)}%`
}

export function formatDate(iso: string): string {
  return new Date(iso).toLocaleString('tr-TR', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  })
}

/**
 * A scenario metric rendered according to its free-text unit. Unknown units are shown
 * verbatim next to the number rather than guessed at.
 */
export function formatMetric(value: number | null | undefined, unit: string): string {
  if (value === null || value === undefined || !Number.isFinite(value)) return '—'
  switch (unit) {
    case 'ratio':
      return `${numberFormat.format(value * 100)}%`
    case 'ms':
      return value >= 10_000 ? `${numberFormat.format(value / 1000)} s` : `${numberFormat.format(value)} ms`
    case 's':
      return `${numberFormat.format(value)} s`
    case 'USD':
      return `$${new Intl.NumberFormat('tr-TR', { maximumFractionDigits: value < 1 ? 4 : 2 }).format(value)}`
    case 'bytes':
      return formatBytes(value)
    case '':
    case 'count':
      return numberFormat.format(value)
    case 'users':
      return `${numberFormat.format(value)} kullanıcı`
    case 'tokens':
      return `${numberFormat.format(value)} token`
    default:
      return `${numberFormat.format(value)} ${unit}`
  }
}

/** Human label for a metric name such as "latency.p95" or "tokens.input". */
export function metricLabel(name: string): string {
  const labels: Record<string, string> = {
    accuracy: 'Doğruluk',
    consistency: 'Tutarlılık',
    'pass.rate': 'Başarı oranı',
    'clarification.rate': 'Netleştirme oranı',
    'error.rate': 'Hata oranı',
    'latency.mean': 'Ortalama süre',
    'latency.p50': 'P50 süre',
    'latency.p95': 'P95 süre',
    'latency.p99': 'P99 süre',
    'latency.max': 'En uzun süre',
    'tokens.input': 'Girdi token',
    'tokens.output': 'Çıktı token',
    'tokens.total': 'Toplam token',
    'cost.total': 'Toplam maliyet',
    'cost.per_question': 'Soru başına maliyet',
    throughput: 'Verim',
    requests: 'İstek sayısı',
    questions: 'Soru sayısı',
    attempts: 'Deneme',
    'consistency.result': 'Sonuç tutarlılığı',
    'preparation.mean': 'Ortalama hazırlık',
    'tokens.per_question': 'Soru başına token',
    'concurrency.max_healthy': 'Sağlıklı eşzamanlı kullanıcı',
    'latency.p95.single_user': 'P95 (tek kullanıcı)',
    'scaling.efficiency': 'Ölçeklenme verimi',
    'users.active': 'Aktif kullanıcı',
    'completed.rate': 'Tamamlanma oranı',
    'failed.rate': 'Başarısızlık oranı',
    'reask.rate': 'Tekrar sorma oranı',
    'feedback.count': 'Geri bildirim',
    'feedback.positive.rate': 'Olumlu geri bildirim',
    analyses: 'Analiz sayısı',
    'analysis.success.rate': 'Analiz başarı oranı',
    'analysis.duration.p50': 'Analiz süresi P50',
    'analysis.duration.p95': 'Analiz süresi P95',
    'analysis.tokens.mean': 'Analiz başına token',
    'cost.questions': 'Soru maliyeti',
    'cost.analyses': 'Analiz maliyeti',
  }
  return labels[name] ?? name
}

export function shortName(fullName: string): string {
  return fullName.includes('.') ? fullName.slice(fullName.indexOf('.') + 1) : fullName
}

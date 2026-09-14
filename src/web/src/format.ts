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

export function shortName(fullName: string): string {
  return fullName.includes('.') ? fullName.slice(fullName.indexOf('.') + 1) : fullName
}

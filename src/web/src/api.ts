export type ComparisonStatus =
  | 'Neutral'
  | 'Regression'
  | 'Improvement'
  | 'OnlyInBaseline'
  | 'OnlyInTarget'

export interface RunSummary {
  id: string
  runAt: string
  label: string | null
  machineName: string
  runtimeVersion: string
  buildConfiguration: string
  gitCommit: string | null
  gitBranch: string | null
  totalDurationMs: number
  resultCount: number
}

export interface BenchmarkResult {
  id: string
  typeName: string
  methodName: string
  fullName: string
  parameters: string
  categories: string
  isBaseline: boolean
  success: boolean
  meanNs: number
  errorNs: number
  stdDevNs: number
  medianNs: number
  minNs: number
  maxNs: number
  p95Ns: number
  opsPerSecond: number
  gen0: number
  gen1: number
  gen2: number
  allocatedBytes: number
  ratioToBaseline: number | null
}

export interface RunDetail {
  run: RunSummary
  results: BenchmarkResult[]
}

export interface RunList {
  total: number
  items: RunSummary[]
}

export interface BenchmarkIdentity {
  fullName: string
  typeName: string
  methodName: string
  parameters: string
  runCount: number
  lastSeenAt: string
  lastMeanNs: number
}

export interface HistoryPoint {
  runId: string
  runAt: string
  label: string | null
  gitCommit: string | null
  meanNs: number
  stdDevNs: number
  allocatedBytes: number
}

export interface BenchmarkHistory {
  fullName: string
  points: HistoryPoint[]
}

export interface ComparisonRow {
  fullName: string
  baselineMeanNs: number | null
  targetMeanNs: number | null
  meanDeltaPercent: number | null
  baselineAllocatedBytes: number | null
  targetAllocatedBytes: number | null
  allocatedDeltaPercent: number | null
  status: ComparisonStatus
}

export interface Comparison {
  baseline: RunSummary
  target: RunSummary
  threshold: number
  regressionCount: number
  improvementCount: number
  rows: ComparisonRow[]
}

export interface DashboardSummary {
  runCount: number
  benchmarkCount: number
  resultCount: number
  latestRun: RunSummary | null
  regressionsVsPreviousRun: number
}

async function get<T>(path: string): Promise<T> {
  const response = await fetch(path)
  if (!response.ok) {
    throw new Error(`${response.status} ${response.statusText} — ${path}`)
  }
  return (await response.json()) as T
}

export const api = {
  summary: () => get<DashboardSummary>('/api/summary'),
  runs: (search?: string) =>
    get<RunList>(`/api/runs?take=100${search ? `&search=${encodeURIComponent(search)}` : ''}`),
  run: (id: string) => get<RunDetail>(`/api/runs/${id}`),
  benchmarks: () => get<BenchmarkIdentity[]>('/api/benchmarks'),
  history: (fullName: string) =>
    get<BenchmarkHistory>(`/api/benchmarks/history?fullName=${encodeURIComponent(fullName)}&limit=100`),
  compare: (baselineRunId: string, targetRunId: string, threshold: number) =>
    get<Comparison>(
      `/api/compare?baselineRunId=${baselineRunId}&targetRunId=${targetRunId}&threshold=${threshold}`,
    ),
  deleteRun: async (id: string) => {
    const response = await fetch(`/api/runs/${id}`, { method: 'DELETE' })
    if (!response.ok) {
      throw new Error(`Kosu silinemedi: ${response.status}`)
    }
  },
}

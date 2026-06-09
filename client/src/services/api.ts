import type { ElevatorLine, SiloDetail, SiloSummary, CultureInfo, AlertInfo, HistoryPoint, PollingConfig, SiloDeltaData } from '../models/types'

const BASE = '/api'

async function get<T>(url: string): Promise<T> {
  const res = await fetch(`${BASE}${url}`)
  if (!res.ok) throw new Error(`GET ${url} failed: ${res.status}`)
  return res.json()
}

async function post<T>(url: string, body?: unknown): Promise<T> {
  const res = await fetch(`${BASE}${url}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: body ? JSON.stringify(body) : undefined,
  })
  if (!res.ok) throw new Error(`POST ${url} failed: ${res.status}`)
  return res.json()
}

async function put<T>(url: string, body?: unknown): Promise<T> {
  const res = await fetch(`${BASE}${url}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: body ? JSON.stringify(body) : undefined,
  })
  if (!res.ok) throw new Error(`PUT ${url} failed: ${res.status}`)
  return res.json()
}

export const api = {
  getLines: () => get<ElevatorLine[]>('/line'),
  getSilos: () => get<SiloSummary[]>('/silo'),
  getSilo: (id: number) => get<SiloDetail>(`/silo/${id}`),
  getSiloReadings: (id: number, from?: string, to?: string) => {
    const params = new URLSearchParams()
    if (from) params.set('from', from)
    if (to) params.set('to', to)
    return get<HistoryPoint[]>(`/silo/${id}/readings?${params}`)
  },

  getCultures: () => get<CultureInfo[]>('/culture'),
  updateCulture: (id: number, data: Partial<CultureInfo>) =>
    put<CultureInfo>(`/culture/${id}`, data),

  getAlerts: (activeOnly?: boolean, siloId?: number) => {
    const params = new URLSearchParams()
    if (activeOnly) params.set('activeOnly', 'true')
    if (siloId) params.set('siloId', String(siloId))
    return get<AlertInfo[]>(`/alert?${params}`)
  },
  acknowledgeAlert: (id: number, user?: string) =>
    post(`/alert/${id}/acknowledge`, { user }),
  acknowledgeAllAlerts: (user?: string) =>
    post('/alert/acknowledge-all', { user }),
  resolveAlert: (id: number) =>
    post(`/alert/${id}/resolve`),
  resolveAllAlerts: () =>
    post('/alert/resolve-all'),

  getSummaryReport: async (from: string, to: string, format: 'excel' | 'pdf' = 'excel') => {
    const params = new URLSearchParams({ from, to, format })
    const res = await fetch(`${BASE}/report/summary?${params}`)
    if (!res.ok) throw new Error('Report fetch failed')
    return res.blob()
  },
  getSiloReport: async (id: number, from: string, to: string) => {
    const params = new URLSearchParams({ from, to })
    const res = await fetch(`${BASE}/report/silo/${id}?${params}`)
    if (!res.ok) throw new Error('Report fetch failed')
    return res.blob()
  },
  getTemperatureLog: async (from: string, to: string) => {
    const params = new URLSearchParams({ from, to })
    const res = await fetch(`${BASE}/report/temperature-log?${params}`)
    if (!res.ok) throw new Error('Report fetch failed')
    return res.blob()
  },
  getAlertReport: async (from: string, to: string) => {
    const params = new URLSearchParams({ from, to })
    const res = await fetch(`${BASE}/report/alerts?${params}`)
    if (!res.ok) throw new Error('Report fetch failed')
    return res.blob()
  },

  getScenarios: () => get<string[]>('/emulator/scenarios'),
  setScenario: (siloId: number, scenario: string) =>
    post(`/emulator/scenario/${siloId}`, { scenario }),
  resetEmulator: () => post('/emulator/reset'),

  getPollingConfig: () => get<PollingConfig>('/pollingconfig'),
  updatePollingConfig: (data: Partial<PollingConfig>) =>
    put<PollingConfig>('/pollingconfig', data),

  updateLine: (id: number, data: { name: string }) =>
    put(`/line/${id}`, data),

  configureSilo: (id: number, data: {
    cultureId?: number
    fillLevel?: number
    pendants?: Array<{ positionIndex: number; pointCount: number }>
  }) => put(`/silo/${id}/configure`, data),

  getHardwareConfig: () => get<unknown>('/config/hardware'),
  updateHardwareConfig: (data: unknown) =>
    put('/config/hardware', data),

  getElevatorConfig: () => get<unknown>('/config/elevator'),
  updateElevatorConfig: (data: unknown) =>
    put('/config/elevator', data),

  getSiloDelta: (id: number, hours = 24) =>
    get<SiloDeltaData>(`/silo/${id}/delta?hours=${hours}`),
}

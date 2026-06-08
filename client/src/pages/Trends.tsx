import { useEffect, useState, useMemo } from 'react'
import { useSearchParams } from 'react-router-dom'
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, ReferenceLine } from 'recharts'
import { api } from '../services/api'
import type { SiloDetail, HistoryPoint } from '../models/types'

type TimeRange = '1h' | '6h' | '24h' | '7d'

function timeRangeToMs(range: TimeRange): number {
  switch (range) {
    case '1h': return 3600000
    case '6h': return 21600000
    case '24h': return 86400000
    case '7d': return 604800000
  }
}

export default function Trends() {
  const [searchParams] = useSearchParams()
  const [silos, setSilos] = useState<SiloDetail[]>([])
  const [selectedSilo, setSelectedSilo] = useState<number>(
    Number(searchParams.get('siloId')) || 1
  )
  const [selectedPendant, setSelectedPendant] = useState<number | null>(
    searchParams.get('pendantId') ? Number(searchParams.get('pendantId')) : null
  )
  const [selectedPoint, setSelectedPoint] = useState<number | null>(
    searchParams.get('point') ? Number(searchParams.get('point')) : null
  )
  const [timeRange, setTimeRange] = useState<TimeRange>('24h')
  const [readings, setReadings] = useState<HistoryPoint[]>([])
  const [loading, setLoading] = useState(false)

  useEffect(() => {
    api.getSilos().then(async (summaries) => {
      const details = await Promise.all(
        summaries.map(s => api.getSilo(s.id).catch(() => null))
      )
      setSilos(details.filter(Boolean) as SiloDetail[])
    }).catch(() => {})
  }, [])

  useEffect(() => {
    const now = Date.now()
    const from = new Date(now - timeRangeToMs(timeRange))
    setLoading(true)
    api.getSiloReadings(selectedSilo, from.toISOString(), new Date(now).toISOString())
      .then(setReadings)
      .catch(() => {})
      .finally(() => setLoading(false))
  }, [selectedSilo, timeRange])

  const silo = silos.find(s => s.id === selectedSilo)

  const pendants = useMemo(() => {
    if (!silo) return []
    const seen = new Set<number>()
    return silo.pendants.filter(p => {
      if (seen.has(p.positionIndex)) return false
      seen.add(p.positionIndex)
      return true
    })
  }, [silo])

  const points = useMemo(() => {
    const p = pendants.find(pp => pp.id === selectedPendant)
    return p ? p.points : []
  }, [pendants, selectedPendant])

  const chartData = useMemo(() => {
    if (!readings.length || selectedPendant === null || selectedPoint === null) return []

    const filtered = readings.filter(
      r => r.thermopendantId === selectedPendant && r.pointIndex === selectedPoint && r.isValid
    )

    const bucketMs = timeRange === '7d' ? 3600000 : 60000
    const buckets = new Map<number, { sum: number; count: number; time: number }>()

    for (const r of filtered) {
      const key = Math.floor(new Date(r.timestamp).getTime() / bucketMs) * bucketMs
      const b = buckets.get(key)
      if (b) { b.sum += r.temperature; b.count++ }
      else buckets.set(key, { sum: r.temperature, count: 1, time: key })
    }

    return Array.from(buckets.values())
      .map(b => ({ time: b.time, temp: Math.round(b.sum / b.count * 10) / 10 }))
      .sort((a, b) => a.time - b.time)
  }, [readings, selectedPendant, selectedPoint, timeRange])

  if (!silos.length) {
    return <div className="card" style={{ textAlign: 'center', padding: 40, color: '#6a6e88' }}>Загрузка...</div>
  }

  return (
    <div>
      <div className="page-header">
        <h1>Тренды температуры</h1>
        <p>Графики изменения температуры по точкам измерения</p>
      </div>

      <div className="time-selector">
        {(['1h', '6h', '24h', '7d'] as TimeRange[]).map(t => (
          <button key={t} className={`time-btn ${timeRange === t ? 'active' : ''}`}
            onClick={() => setTimeRange(t)}>
            {t === '1h' ? '1 час' : t === '6h' ? '6 часов' : t === '24h' ? '24 часа' : '7 дней'}
          </button>
        ))}
      </div>

      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginBottom: 12, alignItems: 'center' }}>
        <select className="form-input" style={{ width: 160 }}
          value={selectedSilo} onChange={e => { setSelectedSilo(Number(e.target.value)); setSelectedPendant(null); setSelectedPoint(null) }}>
          {silos.map(s => (
            <option key={s.id} value={s.id}>{s.lineName} / №{s.number}</option>
          ))}
        </select>

        <select className="form-input" style={{ width: 180 }}
          value={selectedPendant ?? ''} onChange={e => { setSelectedPendant(e.target.value ? Number(e.target.value) : null); setSelectedPoint(null) }}>
          <option value="">— Подвеска —</option>
          {pendants.map((p) => {
            const peripheralIdx = pendants.filter(pp => !pp.isCentral).indexOf(p) + 1
            return (
              <option key={p.id} value={p.id}>
                {p.isCentral ? 'Центральная' : `Периферийная #${peripheralIdx}`}
              </option>
            )
          })}
        </select>

        <select className="form-input" style={{ width: 120 }}
          value={selectedPoint ?? ''} onChange={e => setSelectedPoint(e.target.value ? Number(e.target.value) : null)}
          disabled={selectedPendant === null}>
          <option value="">— Точка —</option>
          {points.map(pt => (
            <option key={pt.index} value={pt.index}>Т.{pt.index}</option>
          ))}
        </select>

        {loading && <span style={{ color: '#6a6e88', fontSize: 12 }}>загрузка...</span>}
      </div>

      <div className="chart-container">
        {chartData.length === 0 ? (
          <div style={{ textAlign: 'center', padding: 60, color: '#6a6e88' }}>
            {selectedPendant !== null && selectedPoint !== null
              ? 'Нет данных за выбранный период'
              : 'Выберите подвеску и точку'}
          </div>
        ) : (
          <ResponsiveContainer width="100%" height={400}>
            <LineChart data={chartData}>
              <CartesianGrid strokeDasharray="3 3" stroke="#1e2238" />
              <XAxis
                dataKey="time"
                stroke="#6a6e88" fontSize={11}
                tickFormatter={(ts) => new Date(ts).toLocaleString('ru-RU', {
                  hour: '2-digit', minute: '2-digit',
                  ...(timeRange === '7d' ? { day: '2-digit', month: '2-digit' } : {})
                })}
              />
              <YAxis stroke="#6a6e88" fontSize={11} domain={['auto', 'auto']} />
              <Tooltip
                contentStyle={{ background: '#141828', border: '1px solid #2a2e3e', borderRadius: 4 }}
                labelStyle={{ color: '#8a8fa8' }}
                labelFormatter={(ts) => new Date(ts).toLocaleString('ru-RU')}
              />
              <ReferenceLine y={28} stroke="#e8c84a" strokeDasharray="4 4" label="Пред." />
              <ReferenceLine y={35} stroke="#e84545" strokeDasharray="4 4" label="Крит." />
              <Line
                type="monotone"
                dataKey="temp"
                stroke="#5b8def"
                strokeWidth={2}
                dot={false}
                name={`Т.${selectedPoint}`}
                connectNulls
              />
            </LineChart>
          </ResponsiveContainer>
        )}
      </div>
    </div>
  )
}

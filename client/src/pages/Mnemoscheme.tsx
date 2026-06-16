import { useEffect, useState, useCallback, useRef } from 'react'
import { useNavigate } from 'react-router-dom'
import { api } from '../services/api'
import { onSiloUpdate } from '../services/signalr'
import StatusBar from '../components/StatusBar'
import SiloBody from '../components/SiloBody'
import SiloLayers from '../components/SiloLayers'
import type { ElevatorLine, SiloSummary, SiloDetail, SiloDeltaData } from '../models/types'

type Tab = 'silos' | 'layers' | 'delta'

function tempColorGradient(temp: number): string {
  if (temp <= 10) return '#0066ff'
  if (temp <= 20) return '#33cc33'
  if (temp <= 28) return '#e8c84a'
  if (temp <= 35) return '#ff8800'
  return '#e84545'
}

function alertColor(level: number): string {
  if (level === 7) return '#8b5cf6'
  if (level >= 2) return '#e84545'
  if (level >= 1) return '#ff8800'
  return 'transparent'
}

function deltaColor(delta: number | null): string {
  if (delta == null) return '#1a1e32'
  return delta > 2 ? '#e84545' : '#33cc33'
}

function deltaLabel(delta: number | null): string {
  if (delta == null) return '—'
  return delta.toFixed(1) + '°'
}

export default function Mnemoscheme() {
  const navigate = useNavigate()
  const [lines, setLines] = useState<ElevatorLine[]>([])
  const [silos, setSilos] = useState<SiloSummary[]>([])
  const [siloDetails, setSiloDetails] = useState<Record<number, SiloDetail>>({})
  const [siloDeltas, setSiloDeltas] = useState<Record<number, SiloDeltaData>>({})
  const [activeLine, setActiveLine] = useState(1)
  const [tab, setTab] = useState<Tab>('silos')

  const deltaStats = useRef<Record<string, { min: number; max: number }>>({})

  useEffect(() => {
    api.getLines().then(setLines).catch(() => {})
    api.getSilos().then(setSilos).catch(() => {})
  }, [])

  const fetchDetails = useCallback(async () => {
    const line = lines.find(l => l.displayOrder === activeLine)
    if (!line) return
    const lineSilos = silos.filter(s => s.lineId === line.id)
    for (const s of lineSilos) {
      if (!siloDetails[s.id]) {
        try {
          const d = await api.getSilo(s.id)
          setSiloDetails(prev => ({ ...prev, [s.id]: d }))
        } catch { }
      }
    }
  }, [lines, activeLine, silos, siloDetails])

  const fetchDeltas = useCallback(async () => {
    const line = lines.find(l => l.displayOrder === activeLine)
    if (!line) return
    const lineSilos = silos.filter(s => s.lineId === line.id)
    const next: Record<number, SiloDeltaData> = {}
    for (const s of lineSilos) {
      try {
        const d = await api.getSiloDelta(s.id)
        next[s.id] = d
        for (const p of d.pendants) {
          for (const pt of p.points) {
            if (pt.minTemp != null && pt.maxTemp != null) {
              deltaStats.current[`${p.id}_${pt.pointIndex}`] = { min: pt.minTemp, max: pt.maxTemp }
            }
          }
        }
      } catch { }
    }
    setSiloDeltas(prev => ({ ...prev, ...next }))
  }, [lines, activeLine, silos])

  useEffect(() => {
    fetchDetails()
  }, [activeLine, fetchDetails])

  useEffect(() => {
    if (tab === 'delta') fetchDeltas()
  }, [tab, fetchDeltas])

  useEffect(() => {
    if (tab !== 'delta') return
    const interval = setInterval(fetchDeltas, 30000)
    return () => clearInterval(interval)
  }, [tab, fetchDeltas])

  useEffect(() => {
    return onSiloUpdate((data: unknown) => {
      const updates = data as Array<{
        siloId: number
        maxTemp: number
        avgTemp: number
        hasActiveAlert: boolean
        alertLevel: number
        pendants: Array<{
          pendantId: number
          position: number
          maxTemp: number
          points: Array<{ isValid: boolean; pointIndex: number; temp: number; humidity: number | null }>
        }>
      }>
      if (!Array.isArray(updates)) return

      setSilos(prev => prev.map(s => {
        const u = updates.find(x => x.siloId === s.id)
        if (!u) return s
        return { ...s, maxTemp: u.maxTemp, avgTemp: u.avgTemp, hasActiveAlert: u.hasActiveAlert, alertLevel: u.alertLevel }
      }))

      setSiloDetails(prev => {
        const next = { ...prev }
        for (const u of updates) {
          if (next[u.siloId]) {
            next[u.siloId] = {
              ...next[u.siloId],
              pendants: next[u.siloId].pendants.map(p => {
                const pu = u.pendants.find(x => x.pendantId === p.id)
                if (!pu) return p
                return {
                  ...p,
                  points: p.points.map(pt => {
                    const up = pu.points.find(x => x.pointIndex === pt.index)
                    if (!up) return pt
                    return {
                      ...pt,
                      isValid: up.isValid,
                      temp: up.isValid ? up.temp : pt.temp,
                      humidity: up.isValid ? up.humidity : pt.humidity
                    }
                  })
                }
              })
            }
          }
        }
        return next
      })

      let statsChanged = false
      for (const u of updates) {
        for (const pu of u.pendants) {
          for (const up of pu.points) {
            if (!up.isValid) continue
            const key = `${pu.pendantId}_${up.pointIndex}`
            const existing = deltaStats.current[key]
            if (existing) {
              if (up.temp < existing.min) { existing.min = up.temp; statsChanged = true }
              if (up.temp > existing.max) { existing.max = up.temp; statsChanged = true }
            } else {
              deltaStats.current[key] = { min: up.temp, max: up.temp }
              statsChanged = true
            }
          }
        }
      }

      if (statsChanged && tab === 'delta') {
        setSiloDeltas(prev => {
          const next = { ...prev }
          for (const u of updates) {
            const existing = next[u.siloId]
            if (!existing) continue
            next[u.siloId] = {
              ...existing,
              pendants: existing.pendants.map(p => {
                const pu = u.pendants.find(x => x.pendantId === p.id)
                if (!pu) return p
                return {
                  ...p,
                  points: p.points.map(pt => {
                    const key = `${p.id}_${pt.pointIndex}`
                    const stats = deltaStats.current[key]
                    if (!stats) return pt
                    const delta = Math.round((stats.max - stats.min) * 10) / 10
                    return { ...pt, delta, minTemp: stats.min, maxTemp: stats.max }
                  })
                }
              })
            }
          }
          return next
        })
      }
    })
  }, [tab])

  const currentLine = lines.find(l => l.displayOrder === activeLine)
  const currentLineSilos = silos.filter(s => s.lineId === currentLine?.id).sort((a, b) => a.number - b.number)

  function computeLayerAverages(detail: SiloDetail): Array<{ index: number; avgTemp: number; pointCount: number }> {
    const allPoints = detail.pendants.flatMap(p => p.points.filter(pt => pt.isValid && pt.temp !== null))
    const byIndex: Record<number, { sum: number; count: number }> = {}
    for (const pt of allPoints) {
      if (!byIndex[pt.index]) byIndex[pt.index] = { sum: 0, count: 0 }
      byIndex[pt.index].sum += pt.temp!
      byIndex[pt.index].count++
    }
    const maxLayer = Math.max(...detail.pendants.map(p => p.pointCount))
    return Array.from({ length: maxLayer }, (_, i) => ({
      index: i,
      avgTemp: byIndex[i] ? byIndex[i].sum / byIndex[i].count : 0,
      pointCount: byIndex[i]?.count ?? 0
    }))
  }

  return (
    <div>
      <StatusBar />

      <div className="gradient-legend">
        <span>0°C</span>
        <div className="gradient-bar" />
        <span>50°C</span>
      </div>

      <div style={{ display: 'flex', gap: 8, marginBottom: 16, alignItems: 'center' }}>
        <div className="line-tabs">
          {[1, 2, 3].map(n => (
            <button key={n}
              className={`line-tab ${activeLine === n ? 'active' : ''}`}
              onClick={() => setActiveLine(n)}>
              Линия {n}
            </button>
          ))}
        </div>
        <div className="tab-switch" style={{ marginLeft: 'auto', display: 'flex', gap: 4 }}>
          <button className={`time-btn ${tab === 'silos' ? 'active' : ''}`}
            onClick={() => setTab('silos')}>Силосы</button>
          <button className={`time-btn ${tab === 'layers' ? 'active' : ''}`}
            onClick={() => setTab('layers')}>Слои</button>
          <button className={`time-btn ${tab === 'delta' ? 'active' : ''}`}
            onClick={() => setTab('delta')}>Уровень</button>
        </div>
      </div>

      {tab === 'silos' && (
        <div className="line-silos-large">
          {currentLineSilos.map(silo => {
            const detail = siloDetails[silo.id]
            return (
              <div key={silo.id} className="silo-large-card" style={{
                cursor: 'pointer',
                borderLeft: silo.hasActiveAlert ? `4px solid ${alertColor(silo.alertLevel)}` : '2px solid #1e2238'
              }}
                onClick={() => navigate(`/silo/${silo.id}`)}>
                <div className="silo-large-header">
                  <span className="silo-large-num">Силос №{silo.number}</span>
                  <span className="silo-large-culture">{silo.cultureName}</span>
                  {silo.hasActiveAlert && (
                    <span className="alert-dot" style={{ background: alertColor(silo.alertLevel) }} title="Есть активные алармы" />
                  )}
                  <span className="silo-large-temp" style={{ color: tempColorGradient(silo.maxTemp) }}>
                    {silo.maxTemp.toFixed(1)}°C
                  </span>
                </div>
                <div className="silo-large-body" onClick={e => e.stopPropagation()}>
                  {detail ? (
                    <SiloBody
                      pendants={detail.pendants}
                      siloId={silo.id}
                      onPointClick={(sid, pendantId, pointIdx) =>
                        navigate(`/trends?siloId=${sid}&pendantId=${pendantId}&point=${pointIdx}`)
                      }
                    />
                  ) : (
                    <div style={{ padding: 20, textAlign: 'center', color: '#6a6e88' }}>Загрузка...</div>
                  )}
                </div>
                <div className="silo-large-footer">
                  Загрузка: {silo.fillLevel.toFixed(0)}% | Средняя: {silo.avgTemp.toFixed(1)}°C
                </div>
              </div>
            )
          })}
        </div>
      )}

      {tab === 'layers' && (
        <div className="line-silos-large">
          {currentLineSilos.map(silo => {
            const detail = siloDetails[silo.id]
            const layers = detail ? computeLayerAverages(detail) : []
            const maxPoints = detail ? Math.max(...detail.pendants.map(p => p.pointCount)) : 1
            const segHeight = Math.max(12, 460 / maxPoints)
            return (
              <div key={silo.id} className="silo-large-card" style={{
                borderLeft: silo.hasActiveAlert ? `4px solid ${alertColor(silo.alertLevel)}` : '2px solid #1e2238'
              }}>
                <div className="silo-large-header">
                  <span className="silo-large-num">Силос №{silo.number}</span>
                  <span className="silo-large-culture">{silo.cultureName}</span>
                  {silo.hasActiveAlert && (
                    <span className="alert-dot" style={{ background: alertColor(silo.alertLevel) }} title="Есть активные алармы" />
                  )}
                  <span className="silo-large-temp" style={{ color: tempColorGradient(silo.maxTemp) }}>
                    {silo.maxTemp.toFixed(1)}°C
                  </span>
                </div>
                <div className="silo-large-body" style={{ maxHeight: 400, overflowY: 'auto' }}>
                  {detail ? (
                    <SiloLayers layers={layers} segmentHeight={segHeight} />
                  ) : (
                    <div style={{ padding: 20, textAlign: 'center', color: '#6a6e88' }}>Загрузка...</div>
                  )}
                </div>
              </div>
            )
          })}
        </div>
      )}

      {tab === 'delta' && (
        <div className="line-silos-large">
          {currentLineSilos.map(silo => {
            const deltaData = siloDeltas[silo.id]
            return (
              <div key={silo.id} className="silo-large-card" style={{
                borderLeft: silo.hasActiveAlert ? `4px solid ${alertColor(silo.alertLevel)}` : '2px solid #1e2238'
              }}>
                <div className="silo-large-header">
                  <span className="silo-large-num">Силос №{silo.number}</span>
                  <span className="silo-large-culture">{silo.cultureName}</span>
                  {silo.hasActiveAlert && (
                    <span className="alert-dot" style={{ background: alertColor(silo.alertLevel) }} title="Есть активные алармы" />
                  )}
                </div>
                <div style={{ padding: '4px 0', fontSize: 11, color: '#6a6e88' }}>
                  Перепад макс-мин за {deltaData?.hours ?? 24}ч · <span style={{ color: '#33cc33' }}>зелёный</span> &le;2°C · <span style={{ color: '#e84545' }}>красный</span> &gt;2°C
                </div>
                <div className="silo-large-body" onClick={e => e.stopPropagation()}>
                  {deltaData && deltaData.pendants.length > 0 ? (
                    (() => {
                      const maxPoints = Math.max(...deltaData.pendants.map(p => p.pointCount))
                      const segHeight = Math.max(12, 460 / maxPoints)
                      return (
                        <div className="pendant-bars">
                          {deltaData.pendants.map(pendant => (
                            <div key={pendant.id} className="pendant-bar" title={`Подвеска #${pendant.positionIndex}${pendant.isCentral ? ' (центр)' : ''}`}>
                              <div className="pendant-label">{pendant.isCentral ? 'Ц' : pendant.positionIndex}</div>
                              <div style={{ display: 'flex', flexDirection: 'column-reverse', gap: 2, position: 'relative' }}>
                                {Array.from({ length: pendant.pointCount }, (_, i) => {
                                  const pt = pendant.points.find(p => p.pointIndex === i)
                                  const d = pt?.delta ?? null
                                  return (
                                    <div
                                      key={i}
                                      className="temp-segment"
                                      style={{ height: segHeight, background: deltaColor(d) }}
                                      title={`Точка ${i}: ${deltaLabel(d)}${pt?.minTemp != null && pt?.maxTemp != null ? ` (мин ${pt.minTemp.toFixed(1)}°, макс ${pt.maxTemp.toFixed(1)}°)` : ' нет данных'}`}
                                    />
                                  )
                                })}
                              </div>
                            </div>
                          ))}
                        </div>
                      )
                    })()
                  ) : !deltaData ? (
                    <div style={{ padding: 20, textAlign: 'center', color: '#6a6e88' }}>Загрузка...</div>
                  ) : (
                    <div style={{ padding: 20, textAlign: 'center', color: '#6a6e88' }}>Нет данных за {deltaData.hours}ч</div>
                  )}
                </div>
                <div className="silo-large-footer">
                  {silo.fillLevel > 0 ? `Загрузка: ${silo.fillLevel.toFixed(0)}%` : ''}
                  {deltaData ? ` · Перепад за ${deltaData.hours}ч` : ''}
                </div>
              </div>
            )
          })}
        </div>
      )}
    </div>
  )
}

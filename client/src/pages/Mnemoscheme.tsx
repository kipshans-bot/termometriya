import { useEffect, useState, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { api } from '../services/api'
import { onSiloUpdate } from '../services/signalr'
import StatusBar from '../components/StatusBar'
import SiloBody from '../components/SiloBody'
import SiloLayers from '../components/SiloLayers'
import type { ElevatorLine, SiloSummary, SiloDetail } from '../models/types'

type Tab = 'silos' | 'layers'

function tempColorGradient(temp: number): string {
  if (temp <= 10) return '#0066ff'
  if (temp <= 20) return '#33cc33'
  if (temp <= 28) return '#e8c84a'
  if (temp <= 35) return '#ff8800'
  return '#e84545'
}

export default function Mnemoscheme() {
  const navigate = useNavigate()
  const [lines, setLines] = useState<ElevatorLine[]>([])
  const [silos, setSilos] = useState<SiloSummary[]>([])
  const [siloDetails, setSiloDetails] = useState<Record<number, SiloDetail>>({})
  const [activeLine, setActiveLine] = useState(1)
  const [tab, setTab] = useState<Tab>('silos')

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

  useEffect(() => {
    fetchDetails()
  }, [activeLine, fetchDetails])

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
          points: Array<{ index: number; temp: number; humidity: number | null }>
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
                  points: p.points.map((pt, i) => ({
                    ...pt,
                    temp: pu.points[i]?.temp ?? pt.temp,
                    humidity: pu.points[i]?.humidity ?? pt.humidity
                  }))
                }
              })
            }
          }
        }
        return next
      })
    })
  }, [])

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
        </div>
      </div>

      {tab === 'silos' && (
        <div className="line-silos-large">
          {currentLineSilos.map(silo => {
            const detail = siloDetails[silo.id]
            return (
              <div key={silo.id} className="silo-large-card" style={{ cursor: 'pointer' }}
                onClick={() => navigate(`/silo/${silo.id}`)}>
                <div className="silo-large-header">
                  <span className="silo-large-num">Силос №{silo.number}</span>
                  <span className="silo-large-culture">{silo.cultureName}</span>
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
              <div key={silo.id} className="silo-large-card">
                <div className="silo-large-header">
                  <span className="silo-large-num">Силос №{silo.number}</span>
                  <span className="silo-large-culture">{silo.cultureName}</span>
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
    </div>
  )
}

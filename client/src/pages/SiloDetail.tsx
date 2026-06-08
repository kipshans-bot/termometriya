import { useEffect, useState, useCallback } from 'react'
import { useParams, Link } from 'react-router-dom'
import { api } from '../services/api'
import { onSiloUpdate } from '../services/signalr'
import { tempColor, tempColorHex } from '../utils/gradient'
import SiloBody from '../components/SiloBody'
import SiloLayers from '../components/SiloLayers'
import SiloTopView from '../components/SiloTopView'
import type { SiloDetail as SiloDetailType, AlertInfo } from '../models/types'

const alertTypeLabel: Record<number, string> = {
  1: 'Предупреждение', 2: 'Критическая', 3: 'Градиент (пред.)',
  4: 'Градиент (крит.)', 5: 'Девиация', 6: 'Влажность', 7: 'Неисправность'
}

export default function SiloDetail() {
  const { id } = useParams<{ id: string }>()
  const [data, setData] = useState<SiloDetailType | null>(null)
  const [alerts, setAlerts] = useState<AlertInfo[]>([])
  const [selectedPendant, setSelectedPendant] = useState<number | null>(null)

  const fetchData = useCallback(async () => {
    if (!id) return
    try {
      const d = await api.getSilo(Number(id))
      setData(d)
      setAlerts(d.alerts ?? [])
      if (d.pendants.length > 0 && selectedPendant === null)
        setSelectedPendant(d.pendants[0].id)
    } catch { }
  }, [id, selectedPendant])

  useEffect(() => { fetchData() }, [fetchData])
  useEffect(() => {
    if (!id) return
    const unsub = onSiloUpdate((_data: unknown) => { fetchData() })
    const interval = setInterval(fetchData, 10000)
    return () => { unsub(); clearInterval(interval) }
  }, [id, fetchData])

  if (!data) {
    return <div className="card" style={{ textAlign: 'center', padding: 40, color: '#6a6e88' }}>Загрузка...</div>
  }

  const avgTemp = data.pendants
    .flatMap(p => p.points.filter(pt => pt.isValid && pt.temp !== null))
    .reduce((acc, pt, _, arr) => acc + pt.temp! / arr.length, 0)

  const selectedPendantObj = data.pendants.find(p => p.id === selectedPendant)

  return (
    <div>
      <div className="page-header">
        <Link to="/" style={{ color: '#5b8def', fontSize: 13, textDecoration: 'none' }}>← На мнемосхему</Link>
        <h1>{data.lineName} — Силос №{data.number}</h1>
        <p>Культура: {data.cultureName} | Загрузка: {data.fillLevel.toFixed(0)}% | Средняя T: {avgTemp.toFixed(1)}°C</p>
      </div>

      <div className="silo-detail">
        <div className="silo-body-container">
          <div className="card-title">Температурные колонки по подвескам</div>
          <SiloBody pendants={data.pendants} siloId={data.id} selectedPendantId={selectedPendant ?? undefined} grainLevelPointIndex={data.grainLevelPointIndex} />

          <div style={{ marginTop: 16 }}>
            <div className="card-title">Слои термоподвески</div>
            <select
              className="form-input"
              style={{ width: 240, marginBottom: 8 }}
              value={selectedPendant ?? ''}
              onChange={e => setSelectedPendant(e.target.value ? Number(e.target.value) : null)}
            >
              <option value="">Выберите подвеску</option>
              {data.pendants.map(p => {
                const peripheralIdx = data.pendants.filter(pp => !pp.isCentral).indexOf(p) + 1
                return (
                  <option key={p.id} value={p.id}>
                    {p.isCentral ? 'Центральная' : `Периферийная #${peripheralIdx}`}
                  </option>
                )
              })}
            </select>

            <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
              <div>
                <SiloTopView
                  pendants={data.pendants.map(p => ({ positionIndex: p.positionIndex, pointCount: p.pointCount, isCentral: p.isCentral }))}
                  selectedIndex={selectedPendantObj?.positionIndex ?? null}
                  onSelect={(posIdx) => {
                    const match = data.pendants.find(p => p.positionIndex === posIdx)
                    if (match) setSelectedPendant(match.id)
                  }}
                  siloNumber={data.number}
                />
              </div>
              {selectedPendantObj && (() => {
                const layers = selectedPendantObj.points.map(pt => ({
                  index: pt.index,
                  avgTemp: pt.temp ?? 0,
                  pointCount: pt.isValid ? 1 : 0
                }))
                const maxPoints = Math.max(...data.pendants.map(p => p.pointCount))
                const segHeight = Math.max(12, 460 / maxPoints)
                return <div style={{ flex: 1, minWidth: 200 }}>
                  <SiloLayers layers={layers} segmentHeight={segHeight} />
                </div>
              })()}
            </div>
          </div>
        </div>

        <div className="alerts-list">
          <div className="card-title">Активные алармы</div>
          {alerts.length === 0 ? (
            <div style={{ color: '#6a6e88', fontSize: 13, padding: 8 }}>Нет активных алармов</div>
          ) : (
            alerts.map(a => (
              <div key={a.id} className="alert-item">
              <span className="alert-badge"
                style={{ background: tempColor(a.value, 0.25), color: tempColorHex(a.value), borderColor: tempColorHex(a.value) }}>
                {alertTypeLabel[a.alertType] ?? `Тип ${a.alertType}`}
              </span>
                <div>
                  <div>{a.message}</div>
                  <div style={{ fontSize: 11, color: '#6a6e88' }}>
                    {new Date(a.timestamp).toLocaleString('ru-RU')}
                  </div>
                </div>
              </div>
            ))
          )}
        </div>
      </div>
    </div>
  )
}

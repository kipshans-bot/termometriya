import { useEffect, useState } from 'react'
import { onAlertCounts } from '../services/signalr'
import { tempColorHex } from '../utils/gradient'
import { api } from '../services/api'
import type { AlertCounts, ElevatorLine } from '../models/types'

export default function StatusBar() {
  const [lines, setLines] = useState<ElevatorLine[]>([])
  const [counts, setCounts] = useState<AlertCounts>({ critical: 0, warning: 0, total: 0 })
  const [time, setTime] = useState(new Date())

  useEffect(() => {
    api.getLines().then(setLines).catch(() => {})
    const interval = setInterval(() => setTime(new Date()), 1000)
    return () => clearInterval(interval)
  }, [])

  useEffect(() => {
    return onAlertCounts(data => setCounts(data))
  }, [])

  const siloCount = lines.reduce((sum, l) => sum + l.silos.length, 0)

  return (
    <div className="status-bar">
      <span className="stat">Линии: {lines.length}</span>
      <span className="stat">Силосы: {siloCount}</span>
      <span className="stat">
        <span className="dot" style={{ background: tempColorHex(45) }} /> Крит.: {counts.critical}
      </span>
      <span className="stat">
        <span className="dot" style={{ background: tempColorHex(25) }} /> Пред.: {counts.warning}
      </span>
      <span style={{ marginLeft: 'auto' }}>
        {time.toLocaleString('ru-RU')}
      </span>
    </div>
  )
}

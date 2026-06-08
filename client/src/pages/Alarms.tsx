import { useEffect, useState, useCallback } from 'react'
import { api } from '../services/api'
import { tempColor, tempColorHex } from '../utils/gradient'
import type { AlertInfo } from '../models/types'

const alertTypeLabel: Record<number, string> = {
  1: 'Предупреждение', 2: 'Критическая', 3: 'Градиент (пред.)',
  4: 'Градиент (крит.)', 5: 'Девиация', 6: 'Влажность', 7: 'Неисправность'
}

export default function Alarms() {
  const [alerts, setAlerts] = useState<AlertInfo[]>([])
  const [filter, setFilter] = useState<'active' | 'critical' | 'all'>('active')

  const fetchAlerts = useCallback(async () => {
    try {
      const data = await api.getAlerts(filter === 'active' || filter === 'critical')
      setAlerts(filter === 'critical'
        ? data.filter(a => a.alertType === 2 || a.alertType === 4)
        : data)
    } catch { }
  }, [filter])

  useEffect(() => { fetchAlerts() }, [fetchAlerts])
  useEffect(() => {
    const interval = setInterval(fetchAlerts, 10000)
    return () => clearInterval(interval)
  }, [fetchAlerts])

  const unacknowledgedCount = alerts.filter(a => a.isActive && !a.acknowledgedAt).length

  const acknowledge = async (id: number) => {
    try { await api.acknowledgeAlert(id); fetchAlerts() } catch { }
  }

  const acknowledgeAll = async () => {
    try { await api.acknowledgeAllAlerts(); fetchAlerts() } catch { }
  }

  const resolve = async (id: number) => {
    try { await api.resolveAlert(id); fetchAlerts() } catch { }
  }

  const resolveAll = async () => {
    try { await api.resolveAllAlerts(); fetchAlerts() } catch { }
  }

  return (
    <div>
      <div className="page-header">
        <h1>Журнал алармов</h1>
        <p>Система оповещений о критических ситуациях</p>
      </div>

      <div style={{ display: 'flex', gap: 8, marginBottom: 12, alignItems: 'center' }}>
        {(['active', 'critical', 'all'] as const).map(f => (
          <button key={f} className={`time-btn ${filter === f ? 'active' : ''}`}
            onClick={() => setFilter(f)}>
            {f === 'active' ? 'Активные' : f === 'critical' ? 'Критические' : 'Все'}
            {f === 'active' && unacknowledgedCount > 0 && (
              <span className="alert-badge" style={{
                marginLeft: 6, padding: '1px 6px', fontSize: 11,
                background: tempColorHex(45), color: '#fff'
              }}>!{unacknowledgedCount}</span>
            )}
          </button>
        ))}
        {unacknowledgedCount > 0 && (
          <button className="btn btn-sm" onClick={acknowledgeAll}>
            Подтвердить все
          </button>
        )}
        <button className="btn btn-sm btn-danger" style={{ marginLeft: unacknowledgedCount > 0 ? 4 : 'auto' }} onClick={resolveAll}>
          Сбросить все
        </button>
      </div>

      <div className="card" style={{ padding: 0 }}>
        {alerts.length === 0 ? (
          <div style={{ textAlign: 'center', padding: 24, color: '#6a6e88' }}>Нет алармов</div>
        ) : (
          <table className="data-table">
            <thead>
              <tr>
                <th>Время</th>
                <th>Линия</th>
                <th>Силос</th>
                <th>Тип</th>
                <th>Сообщение</th>
                <th>Значение</th>
                <th>Статус</th>
                <th>Действия</th>
              </tr>
            </thead>
            <tbody>
              {alerts.map(a => (
                <tr key={a.id}>
                  <td style={{ whiteSpace: 'nowrap' }}>
                    {new Date(a.timestamp).toLocaleString('ru-RU')}
                  </td>
                  <td>{a.lineName}</td>
                  <td>№{a.siloNumber}</td>
                  <td>
                    <span className="alert-badge"
                      style={{ background: tempColor(a.value, 0.25), color: tempColorHex(a.value), borderColor: tempColorHex(a.value) }}>
                      {alertTypeLabel[a.alertType] ?? `Тип ${a.alertType}`}
                    </span>
                  </td>
                  <td style={{ maxWidth: 300, overflow: 'hidden', textOverflow: 'ellipsis' }}>
                    {!a.acknowledgedAt && a.isActive && <span style={{ color: '#e8c84a', marginRight: 4 }}>⚠</span>}
                    {a.message}
                  </td>
                  <td>{a.value.toFixed(1)}°C</td>
                  <td>
                    {a.isActive
                      ? a.acknowledgedAt ? 'Подтверждён' : 'Активен'
                      : 'Завершён'}
                  </td>
                  <td>
                    {a.isActive && !a.acknowledgedAt && (
                      <button className="btn btn-sm btn-primary"
                        onClick={() => acknowledge(a.id)}>Подтв.</button>
                    )}
                    {a.isActive && (
                      <button className="btn btn-sm btn-danger"
                        style={{ marginLeft: 4 }}
                        onClick={() => resolve(a.id)}>Сброс</button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  )
}

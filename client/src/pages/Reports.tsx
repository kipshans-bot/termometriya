import { useState } from 'react'
import { api } from '../services/api'

type ReportType = 'summary-excel' | 'summary-pdf' | 'silo-pdf' | 'temp-log' | 'alert-report'

function formatDate(d: Date): string {
  return d.toISOString().slice(0, 16)
}

export default function Reports() {
  const [from, setFrom] = useState(formatDate(new Date(Date.now() - 7 * 86400000)))
  const [to, setTo] = useState(formatDate(new Date()))
  const [siloId, setSiloId] = useState(1)
  const [loading, setLoading] = useState<string | null>(null)

  const downloadBlob = (blob: Blob, filename: string) => {
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = filename
    a.click()
    URL.revokeObjectURL(url)
  }

  const generate = async (type: ReportType) => {
    setLoading(type)
    try {
      const fromISO = new Date(from).toISOString()
      const toISO = new Date(to).toISOString()
      const dateStr = `${fromISO.slice(0, 10)}_${toISO.slice(0, 10)}`

      switch (type) {
        case 'summary-excel': {
          const blob = await api.getSummaryReport(fromISO, toISO, 'excel')
          downloadBlob(blob, `summary_${dateStr}.xlsx`)
          break
        }
        case 'summary-pdf': {
          const blob = await api.getSummaryReport(fromISO, toISO, 'pdf')
          downloadBlob(blob, `summary_${dateStr}.pdf`)
          break
        }
        case 'silo-pdf': {
          const blob = await api.getSiloReport(siloId, fromISO, toISO)
          downloadBlob(blob, `silo_${siloId}_${dateStr}.pdf`)
          break
        }
        case 'temp-log': {
          const blob = await api.getTemperatureLog(fromISO, toISO)
          downloadBlob(blob, `temperature_log_${dateStr}.csv`)
          break
        }
        case 'alert-report': {
          const blob = await api.getAlertReport(fromISO, toISO)
          downloadBlob(blob, `alert_report_${dateStr}.csv`)
          break
        }
      }
    } catch { }
    setLoading(null)
  }

  return (
    <div>
      <div className="page-header">
        <h1>Отчёты</h1>
        <p>Формирование отчётов по температурному режиму</p>
      </div>

      <div className="card">
        <div style={{ display: 'flex', gap: 12, alignItems: 'end', flexWrap: 'wrap' }}>
          <div className="form-group" style={{ margin: 0 }}>
            <label>Дата с</label>
            <input type="datetime-local" value={from} onChange={e => setFrom(e.target.value)}
              className="form-input" />
          </div>
          <div className="form-group" style={{ margin: 0 }}>
            <label>Дата по</label>
            <input type="datetime-local" value={to} onChange={e => setTo(e.target.value)}
              className="form-input" />
          </div>
          <div className="form-group" style={{ margin: 0 }}>
            <label>Силос (для отчёта по силосу)</label>
            <input type="number" min={1} max={12} value={siloId}
              onChange={e => setSiloId(Number(e.target.value))}
              className="form-input" style={{ width: 70 }} />
          </div>
        </div>
      </div>

      <div className="report-grid">
        <div className="report-card" onClick={() => generate('summary-excel')}>
          <h3>Сводный отчёт (Excel)</h3>
          <p>Средние, максимальные и минимальные температуры по всем силосам за период</p>
          {loading === 'summary-excel' && <div style={{ color: '#e8c84a', fontSize: 12 }}>Генерация...</div>}
        </div>
        <div className="report-card" onClick={() => generate('summary-pdf')}>
          <h3>Сводный отчёт (PDF)</h3>
          <p>Таблица температур по силосам в формате PDF</p>
          {loading === 'summary-pdf' && <div style={{ color: '#e8c84a', fontSize: 12 }}>Генерация...</div>}
        </div>
        <div className="report-card" onClick={() => generate('silo-pdf')}>
          <h3>Отчёт по силосу (PDF)</h3>
          <p>Детальный отчёт по одному силосу с таблицей замеров</p>
          {loading === 'silo-pdf' && <div style={{ color: '#e8c84a', fontSize: 12 }}>Генерация...</div>}
        </div>
        <div className="report-card" onClick={() => generate('temp-log')}>
          <h3>Журнал температур (CSV)</h3>
          <p>Все показания температуры и влажности за период</p>
          {loading === 'temp-log' && <div style={{ color: '#e8c84a', fontSize: 12 }}>Генерация...</div>}
        </div>
        <div className="report-card" onClick={() => generate('alert-report')}>
          <h3>Журнал алармов (CSV)</h3>
          <p>Все события алармов за выбранный период</p>
          {loading === 'alert-report' && <div style={{ color: '#e8c84a', fontSize: 12 }}>Генерация...</div>}
        </div>
      </div>
    </div>
  )
}

import { useEffect, useState } from 'react'
import { api } from '../services/api'
import type { CultureInfo } from '../models/types'

export default function ThresholdConfig() {
  const [cultures, setCultures] = useState<CultureInfo[]>([])
  const [saving, setSaving] = useState<Record<number, boolean>>({})

  useEffect(() => {
    api.getCultures().then(setCultures).catch(() => {})
  }, [])

  const updateField = (id: number, field: keyof CultureInfo, value: number | boolean | string) => {
    setCultures(prev => prev.map(c => c.id === id ? { ...c, [field]: value } : c))
  }

  const save = async (id: number) => {
    setSaving(prev => ({ ...prev, [id]: true }))
    try {
      const culture = cultures.find(c => c.id === id)
      if (culture) await api.updateCulture(id, culture)
    } catch { }
    setSaving(prev => ({ ...prev, [id]: false }))
  }

  return (
    <div>
      <div className="page-header">
        <h1>Настройка порогов температур</h1>
        <p>Индивидуальные граничные температуры для каждой культуры</p>
      </div>

      {cultures.map(c => (
        <div key={c.id} className="card">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
            <h3 style={{ fontSize: 16, color: '#e8e8f0' }}>{c.name}</h3>
            <button className={`btn btn-primary btn-sm ${saving[c.id] ? 'disabled' : ''}`}
              onClick={() => save(c.id)} disabled={saving[c.id]}>
              {saving[c.id] ? 'Сохранение...' : 'Сохранить'}
            </button>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(160px, 1fr))', gap: 12 }}>
            <div className="form-group">
              <label>Норма (&le;°C)</label>
              <input type="number" step={0.5} value={c.normTemp}
                onChange={e => updateField(c.id, 'normTemp', Number(e.target.value))}
                className="form-input" />
            </div>
            <div className="form-group">
              <label>Предупреждение (&ge;°C)</label>
              <input type="number" step={0.5} value={c.warnTemp}
                onChange={e => updateField(c.id, 'warnTemp', Number(e.target.value))}
                className="form-input" />
            </div>
            <div className="form-group">
              <label>Критическая (&ge;°C)</label>
              <input type="number" step={0.5} value={c.criticalTemp}
                onChange={e => updateField(c.id, 'criticalTemp', Number(e.target.value))}
                className="form-input" />
            </div>
            <div className="form-group">
              <label>Градиент пред. (°C/сут)</label>
              <input type="number" step={0.1} value={c.gradientWarn}
                onChange={e => updateField(c.id, 'gradientWarn', Number(e.target.value))}
                className="form-input" />
            </div>
            <div className="form-group">
              <label>Градиент крит. (°C/сут)</label>
              <input type="number" step={0.1} value={c.gradientCritical}
                onChange={e => updateField(c.id, 'gradientCritical', Number(e.target.value))}
                className="form-input" />
            </div>
            <div className="form-group">
              <label>Девиация (°C)</label>
              <input type="number" step={0.5} value={c.deviationThreshold}
                onChange={e => updateField(c.id, 'deviationThreshold', Number(e.target.value))}
                className="form-input" />
            </div>
            <div className="form-group">
              <label>Порог высок. T (°C)</label>
              <input type="number" step={0.5} value={c.highTempThreshold}
                onChange={e => updateField(c.id, 'highTempThreshold', Number(e.target.value))}
                className="form-input" />
            </div>
            <div className="form-group">
              <label>Град. при выс. T (°C/сут)</label>
              <input type="number" step={0.5} value={c.highTempGradient}
                onChange={e => updateField(c.id, 'highTempGradient', Number(e.target.value))}
                className="form-input" />
            </div>
            <div className="form-group">
              <label>Порча (&ge;°C)</label>
              <input type="number" step={0.5} value={c.criticalHighTemp50}
                onChange={e => updateField(c.id, 'criticalHighTemp50', Number(e.target.value))}
                className="form-input" />
            </div>
          </div>
        </div>
      ))}
    </div>
  )
}

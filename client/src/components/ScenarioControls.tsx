import { useEffect, useState } from 'react'
import { api } from '../services/api'

export default function ScenarioControls() {
  const [scenarios, setScenarios] = useState<string[]>([])
  const [selectedScenario, setSelectedScenario] = useState('normal')
  const [targetSilo, setTargetSilo] = useState(1)

  useEffect(() => {
    api.getScenarios().then(setScenarios).catch(() => {})
  }, [])

  const apply = async () => {
    try {
      await api.setScenario(targetSilo, selectedScenario)
    } catch { }
  }

  const reset = async () => {
    try {
      await api.resetEmulator()
    } catch { }
  }

  return (
    <div className="card">
      <div className="card-title">Эмулятор (симуляция)</div>
      <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
        <div className="form-group" style={{ margin: 0 }}>
          <label>Силос</label>
          <input type="number" min={1} max={12} value={targetSilo}
            onChange={e => setTargetSilo(Number(e.target.value))}
            className="form-input" style={{ width: 70 }} />
        </div>
        <div className="form-group" style={{ margin: 0 }}>
          <label>Сценарий</label>
          <select value={selectedScenario} onChange={e => setSelectedScenario(e.target.value)}
            className="form-input" style={{ width: 130 }}>
            {scenarios.map(s => (
              <option key={s} value={s}>{s}</option>
            ))}
          </select>
        </div>
        <button className="btn btn-primary btn-sm" onClick={apply}
          style={{ marginTop: 16 }}>Применить</button>
        <button className="btn btn-sm" onClick={reset}
          style={{ marginTop: 16 }}>Сброс</button>
      </div>
    </div>
  )
}

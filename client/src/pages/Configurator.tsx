import { useEffect, useState } from 'react'
import { api } from '../services/api'
import SiloTopView from '../components/SiloTopView'

interface LineCfg { name: string; displayOrder: number; silos: SiloCfg[] }
interface SiloCfg { number: number; fillLevel: number; capacity: number; cultureName: string; pendants: PendantCfg[] }
interface PendantCfg { positionIndex: number; pointCount: number }

interface ElevatorConfig {
  cultures: Array<{ name: string; normTemp: number }>
  lines: LineCfg[]
}

export default function Configurator() {
  const [cfg, setCfg] = useState<ElevatorConfig | null>(null)
  const [selLineIdx, setSelLineIdx] = useState(0)
  const [selSiloNum, setSelSiloNum] = useState<number | null>(null)
  const [selPendIdx, setSelPendIdx] = useState<number | null>(null)
  const [saving, setSaving] = useState(false)
  const [msg, setMsg] = useState('')

  useEffect(() => {
    api.getElevatorConfig().then(d => {
      const data = d as ElevatorConfig
      setCfg(data)
      if (data.lines.length > 0 && data.lines[0].silos.length > 0)
        setSelSiloNum(data.lines[0].silos[0].number)
    }).catch(() => setMsg('Ошибка загрузки конфига'))
  }, [])

  const curLine = cfg?.lines[selLineIdx]
  const curSilos = curLine?.silos ?? []
  const curSilo = curSilos.find(s => s.number === selSiloNum)
  const pendants = curSilo?.pendants ?? []

  const updateLineName = (name: string) => {
    if (!cfg) return
    const lines = [...cfg.lines]
    lines[selLineIdx] = { ...lines[selLineIdx], name }
    setCfg({ ...cfg, lines })
  }

  const updateCulture = (siloNum: number, cultureName: string) => {
    if (!cfg) return
    const lines = [...cfg.lines]
    const silos = lines[selLineIdx].silos.map(s => s.number === siloNum ? { ...s, cultureName } : s)
    lines[selLineIdx] = { ...lines[selLineIdx], silos }
    setCfg({ ...cfg, lines })
  }

  const addPendant = () => {
    if (!curSilo) return
    const isFirstInBlock = curSilo.number % 2 === 1
    const basePos = isFirstInBlock ? 0 : 6
    const used = pendants.filter(p => p.positionIndex >= basePos && p.positionIndex < basePos + 6)
    if (used.length >= 6) { setMsg('Максимум 6 подвесок на силос'); setTimeout(() => setMsg(''), 3000); return }
    const next = basePos + used.length
    if (!cfg) return
    const lines = [...cfg.lines]
    const silos = lines[selLineIdx].silos.map(s =>
      s.number === selSiloNum ? { ...s, pendants: [...s.pendants, { positionIndex: next, pointCount: 30 }] } : s)
    lines[selLineIdx] = { ...lines[selLineIdx], silos }
    setCfg({ ...cfg, lines })
    setSelPendIdx(next)
  }

  const removePendant = (posIdx: number) => {
    if (!cfg) return
    const lines = [...cfg.lines]
    const silos = lines[selLineIdx].silos.map(s =>
      s.number === selSiloNum ? { ...s, pendants: s.pendants.filter(p => p.positionIndex !== posIdx) } : s)
    lines[selLineIdx] = { ...lines[selLineIdx], silos }
    setCfg({ ...cfg, lines })
    if (selPendIdx === posIdx) setSelPendIdx(null)
  }

  const movePendant = (posIdx: number, dir: -1 | 1) => {
    if (!cfg) return
    const arr = [...pendants]
    const idx = arr.findIndex(p => p.positionIndex === posIdx)
    if (idx === -1) return
    const tgt = idx + dir
    if (tgt < 0 || tgt >= arr.length) return
    const tmp = arr[idx]
    arr[idx] = arr[tgt]
    arr[tgt] = tmp
    const lines = [...cfg.lines]
    const silos = lines[selLineIdx].silos.map(s =>
      s.number === selSiloNum ? { ...s, pendants: arr } : s)
    lines[selLineIdx] = { ...lines[selLineIdx], silos }
    setCfg({ ...cfg, lines })
  }

  const updatePointCount = (posIdx: number, v: number) => {
    if (!cfg) return
    const lines = [...cfg.lines]
    const silos = lines[selLineIdx].silos.map(s =>
      s.number === selSiloNum
        ? { ...s, pendants: s.pendants.map(p => p.positionIndex === posIdx ? { ...p, pointCount: v } : p) }
        : s)
    lines[selLineIdx] = { ...lines[selLineIdx], silos }
    setCfg({ ...cfg, lines })
  }

  const saveAll = async () => {
    if (!cfg) return
    setSaving(true)
    try {
      await api.updateElevatorConfig(cfg)
      setMsg('Конфигурация сохранена в elevator-config.jsonc')
    } catch { setMsg('Ошибка сохранения') }
    setSaving(false)
    setTimeout(() => setMsg(''), 3000)
  }

  if (!cfg) return <div className="card" style={{ padding: 40, textAlign: 'center', color: '#6a6e88' }}>Загрузка...</div>

  return (
    <div className="configurator">
      <div className="page-header">
        <h1>Конфигуратор элеватора</h1>
        <p>Настройка линий, силосов и термоподвесок (сохраняется в elevator-config.jsonc)</p>
      </div>
      {msg && <div className="toast" style={{ background: '#1a3a2a', borderColor: '#2a5a3a', marginBottom: 8 }}>{msg}</div>}

      <div className="card" style={{ marginBottom: 12 }}>
        <div className="card-title">1. Названия линий</div>
        {cfg.lines.map((line, i) => (
          <div key={i} style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 4 }}>
            <span style={{ width: 100, color: '#6a6e88', fontSize: 13 }}>Линия {i + 1}:</span>
            <input className="form-input" style={{ flex: 1, maxWidth: 300 }}
              value={line.name}
              onChange={e => { if (selLineIdx === i) updateLineName(e.target.value) }}
            />
          </div>
        ))}
      </div>

      <div style={{ display: 'flex', gap: 12, marginBottom: 12 }}>
        <div className="card" style={{ flex: 1 }}>
          <div className="card-title">2. Продукт в силосах</div>
          <div style={{ display: 'flex', gap: 4, marginBottom: 8 }}>
            {cfg.lines.map((line, i) => (
              <button key={i} className={`time-btn ${selLineIdx === i ? 'active' : ''}`}
                onClick={() => { setSelLineIdx(i); setSelSiloNum(line.silos[0]?.number ?? null); setSelPendIdx(null) }}>
                {line.name}
              </button>
            ))}
          </div>
          {curSilos.map(silo => (
            <div key={silo.number} style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 4, fontSize: 13 }}>
              <span style={{ width: 80, color: '#e8e8f0' }}>№{silo.number}</span>
              <select className="form-input" style={{ flex: 1 }}
                value={silo.cultureName}
                onChange={e => updateCulture(silo.number, e.target.value)}>
                {cfg.cultures.map(c => <option key={c.name} value={c.name}>{c.name}</option>)}
              </select>
            </div>
          ))}
        </div>

        <div className="card" style={{ flex: 1 }}>
          <div className="card-title">3. Связь (RTU / TCP)</div>
          <p style={{ fontSize: 13, color: '#6a6e88' }}>Настройка блоков БКТ-12 — в <code>bkt12-config.jsonc</code></p>
        </div>
      </div>

      <div className="card" style={{ marginBottom: 12 }}>
        <div className="card-title">4. Термоподвески и точки измерения</div>
        <div style={{ display: 'flex', gap: 4, marginBottom: 8, flexWrap: 'wrap' }}>
          {curSilos.map(silo => (
            <button key={silo.number} className={`time-btn ${selSiloNum === silo.number ? 'active' : ''}`}
              onClick={() => { setSelSiloNum(silo.number); setSelPendIdx(null) }}>
              №{silo.number}
            </button>
          ))}
        </div>
        {curSilo && (
          <div style={{ display: 'flex', gap: 16 }}>
            <div style={{ flex: 1 }}>
              {pendants.map(p => (
                <div key={p.positionIndex}
                  className={`cfg-pendant-row ${selPendIdx === p.positionIndex ? 'selected' : ''}`}
                  onClick={() => setSelPendIdx(p.positionIndex)}>
                  <button className="btn btn-xs" style={{ padding: '0 4px', fontSize: 10 }}
                    onClick={e => { e.stopPropagation(); movePendant(p.positionIndex, -1) }}>▲</button>
                  <button className="btn btn-xs" style={{ padding: '0 4px', fontSize: 10 }}
                    onClick={e => { e.stopPropagation(); movePendant(p.positionIndex, 1) }}>▼</button>
                  <span className="cfg-pendant-label">
                    {p.positionIndex % 6 === 0 ? 'Центральная' : `Периферийная #${p.positionIndex % 6}`}
                  </span>
                  <span style={{ fontSize: 12, color: '#6a6e88' }}>{p.pointCount} т.</span>
                  <button className="btn btn-sm btn-danger" style={{ marginLeft: 'auto' }}
                    onClick={e => { e.stopPropagation(); removePendant(p.positionIndex) }}>×</button>
                </div>
              ))}
              <button className="btn btn-sm" style={{ marginTop: 8 }} onClick={addPendant}>+ Добавить подвеску</button>
            </div>
            {selPendIdx !== null && (() => {
              const p = pendants.find(pp => pp.positionIndex === selPendIdx)
              if (!p) return null
              return (
                <div style={{ width: 200 }}>
                  <label style={{ fontSize: 12, color: '#6a6e88', display: 'block', marginBottom: 4 }}>Количество точек</label>
                  <input className="form-input" type="number" min={1} max={50}
                    value={p.pointCount}
                    onChange={e => updatePointCount(selPendIdx, Math.max(1, Math.min(50, Number(e.target.value))))} />
                </div>
              )
            })()}
          </div>
        )}
      </div>

      <div className="card" style={{ marginBottom: 12 }}>
        <div className="card-title">5. Расположение термоподвесок</div>
        {curSilo && pendants.length > 0 ? (
          <div style={{ display: 'flex', gap: 24, alignItems: 'center', justifyContent: 'center' }}>
            <SiloTopView pendants={pendants} selectedIndex={selPendIdx} onSelect={setSelPendIdx} siloNumber={curSilo.number} />
            <div style={{ fontSize: 12, color: '#6a6e88', lineHeight: 1.8 }}>
              <div><span style={{ color: '#e8e8f0' }}>●</span> Центральная подвеска — в центре</div>
              <div><span style={{ color: '#e8e8f0' }}>●</span> Периферийные — равномерно по кругу</div>
            </div>
          </div>
        ) : <div style={{ padding: 20, textAlign: 'center', color: '#6a6e88' }}>Выберите силос с подвесками</div>}
      </div>

      <button className="btn btn-primary" style={{ padding: '10px 24px', fontSize: 15 }} onClick={saveAll} disabled={saving}>
        {saving ? 'Сохранение...' : 'Сохранить конфигурацию в файл'}
      </button>
    </div>
  )
}

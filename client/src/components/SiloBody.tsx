import type { PendantInfo } from '../models/types'
import { tempColor } from '../utils/gradient'

export default function SiloBody({
  pendants,
  siloId,
  selectedPendantId,
  onPointClick,
  grainLevelPointIndex
}: {
  pendants: PendantInfo[]
  siloId?: number
  selectedPendantId?: number
  onPointClick?: (siloId: number, pendantId: number, pointIndex: number) => void
  grainLevelPointIndex?: number
}) {
  if (!pendants || pendants.length === 0) {
    return <div className="card" style={{ textAlign: 'center', padding: 40, color: '#6a6e88' }}>Нет данных</div>
  }

  const maxPoints = Math.max(...pendants.map(p => p.pointCount))
  const sorted = [...pendants].sort((a, b) => a.displayOrder - b.displayOrder)
  const peripheralSorted = sorted.filter(p => !p.isCentral)
  const label = (p: PendantInfo) => {
    if (p.isCentral) return 'Ц'
    const idx = peripheralSorted.indexOf(p) + 1
    return `П${idx}`
  }
  const grainY = grainLevelPointIndex != null
    ? (grainLevelPointIndex / maxPoints) * 100
    : null

  return (
    <div className="pendant-bars" style={{ position: 'relative' }}>
      {grainY != null && (
        <div style={{
          position: 'absolute', left: 0, right: 0, bottom: `${grainY}%`,
          borderTop: '2px dashed rgba(255,255,100,0.8)', zIndex: 5,
          pointerEvents: 'none'
        }} />
      )}
      {sorted.map(pendant => {
        const validPoints = pendant.points.filter(p => p.isValid && p.temp !== null)
        const isSelected = pendant.id === selectedPendantId
        return (
          <div key={pendant.id} className={`pendant-bar${isSelected ? ' selected' : ''}`}>
            <div className="pendant-label">
              {label(pendant)}
            </div>
            <div style={{ display: 'flex', flexDirection: 'column-reverse', gap: 2, position: 'relative' }}>
              {Array.from({ length: pendant.pointCount }, (_, idx) => {
                const point = validPoints.find(p => p.index === idx)
                const temp = point?.temp ?? -1
                const height = Math.max(12, 460 / maxPoints)
                const bg = point ? tempColor(temp) : '#1a1e34'
                return (
                  <div
                    key={idx}
                    className="temp-segment"
                    style={{ height, background: bg, cursor: point && onPointClick ? 'pointer' : 'default' }}
                    title={point ? `Точка ${idx}: ${temp.toFixed(1)}°C` : 'Нет данных'}
                    onClick={point && onPointClick && siloId ? (e) => {
                      e.stopPropagation()
                      onPointClick(siloId, pendant.id, idx)
                    } : undefined}
                  >
                    {point && height >= 14 && (
                      <span style={{ fontSize: Math.min(12, height - 3), fontWeight: 900, color: '#000', lineHeight: 1 }}>
                        {temp.toFixed(1)}
                      </span>
                    )}
                  </div>
                )
              })}
            </div>
          </div>
        )
      })}
    </div>
  )
}

interface LayerData {
  index: number
  avgTemp: number
  pointCount: number
}

function layerColor(temp: number): string {
  const t = Math.max(0, Math.min(50, temp))
  if (t < 5) return 'hsla(220, 100%, 50%, 0.7)'
  if (t < 10) return 'hsla(180, 100%, 50%, 0.7)'
  if (t < 15) return 'hsla(135, 100%, 50%, 0.7)'
  if (t < 20) return 'hsla(100, 87%, 57%, 0.7)'
  if (t < 25) return 'hsla(75, 74%, 51%, 0.7)'
  if (t < 30) return 'hsla(50, 80%, 49%, 0.7)'
  if (t < 35) return 'hsla(35, 100%, 52%, 0.7)'
  if (t < 40) return 'hsla(20, 100%, 52%, 0.7)'
  if (t < 45) return 'hsla(0, 80%, 51%, 0.7)'
  return 'hsla(340, 100%, 35%, 0.7)'
}

export default function SiloLayers({ layers, segmentHeight }: { layers: LayerData[]; segmentHeight?: number }) {
  if (!layers || layers.length === 0) return null

  const maxTemp = Math.max(...layers.map(l => l.avgTemp), 40)
  const h = segmentHeight ?? 16
  const reversed = [...layers].reverse()

  return (
    <div className="layer-bars">
      {reversed.map(layer => {
        const width = Math.max(4, (layer.avgTemp / maxTemp) * 200)
        const color = layerColor(layer.avgTemp)
        return (
          <div key={layer.index} className="layer-bar" style={{ height: h }}>
            <span className="layer-index">{layer.index}</span>
            <div
              className="layer-fill"
              style={{ width, height: h - 4, background: color }}
            />
            <span className="layer-temp">
              {layer.avgTemp.toFixed(1)}°
            </span>
            {layer.pointCount > 0 && (
              <span style={{ fontSize: 10, color: '#4a4e68', marginLeft: 4 }}>
                ({layer.pointCount} т.)
              </span>
            )}
          </div>
        )
      })}
    </div>
  )
}

export type { LayerData }

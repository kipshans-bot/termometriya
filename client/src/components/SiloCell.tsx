import { Link } from 'react-router-dom'
import type { SiloSummary } from '../models/types'

function gradientColor(temp: number): string {
  if (temp <= 10) return '#0066ff'
  if (temp <= 20) return '#33cc33'
  if (temp <= 28) return '#e8c84a'
  if (temp <= 35) return '#ff8800'
  return '#e84545'
}

export default function SiloCell({ silo }: { silo: SiloSummary }) {
  const color = gradientColor(silo.maxTemp)

  return (
    <Link to={`/silo/${silo.id}`} className="silo-cell" style={{ borderColor: color }}>
      <svg className="silo-svg" viewBox="0 0 60 100">
        <ellipse cx="30" cy="8" rx="26" ry="6" fill="#1a2040" stroke={color} strokeWidth="1.5" />
        <rect x="4" y="8" width="52" height="78" fill="#0e1220" stroke={color} strokeWidth="1.5" />
        <ellipse cx="30" cy="86" rx="26" ry="6" fill="#1a2040" stroke={color} strokeWidth="1.5" />
        {silo.fillLevel > 0 && (
          <rect x="6" y={86 - silo.fillLevel * 0.7} width="48" height={silo.fillLevel * 0.7}
            fill={color} fillOpacity="0.15"
          />
        )}
      </svg>
      <div className="silo-number">№{silo.number}</div>
      <div className="silo-culture">{silo.cultureName}</div>
      <div style={{ fontSize: 14, fontWeight: 700, color, marginTop: 2 }}>
        {silo.maxTemp.toFixed(1)}°C
      </div>
    </Link>
  )
}

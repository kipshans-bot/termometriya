interface TopViewPendant {
  positionIndex: number
  pointCount: number
  isCentral?: boolean
}

export default function SiloTopView({
  pendants,
  selectedIndex,
  onSelect,
  siloNumber
}: {
  pendants: TopViewPendant[]
  selectedIndex: number | null
  onSelect: (index: number) => void
  siloNumber: number
}) {
  const cx = 120, cy = 120, r = 100

  const peripheral = pendants.filter(p => !p.isCentral)

  const positions = pendants.map((p) => {
    if (p.isCentral) return { x: cx, y: cy, label: 'Ц' }
    const idx = peripheral.findIndex(pp => pp.positionIndex === p.positionIndex)
    const total = peripheral.length
    const angle = (idx / total) * Math.PI * 2 - Math.PI / 2
    const pr = r * 0.55
    return { x: cx + pr * Math.cos(angle), y: cy + pr * Math.sin(angle), label: `П${idx + 1}` }
  })

  return (
    <svg viewBox="0 0 240 240" className="silo-top-view">
      <circle cx={cx} cy={cy} r={r} fill="none" stroke="#2a3050" strokeWidth="2" />
      <circle cx={cx} cy={cy} r={r * 0.7} fill="none" stroke="#1a1e34" strokeWidth="1" strokeDasharray="4 4" />
      <circle cx={cx} cy={cy} r={r * 0.35} fill="none" stroke="#1a1e34" strokeWidth="1" strokeDasharray="2 3" />
      <text x={cx} y={cy - r - 10} textAnchor="middle" fill="#8a8fa8" fontSize="11">
        Силос №{siloNumber} — вид сверху
      </text>
      {pendants.map((p, i) => {
        const pos = positions[i]
        const isSelected = selectedIndex === p.positionIndex
        return (
          <g key={p.positionIndex} onClick={() => onSelect(p.positionIndex)} style={{ cursor: 'pointer' }}>
            <circle
              cx={pos.x} cy={pos.y} r={14}
              fill={isSelected ? '#3a5090' : '#1a2240'}
              stroke={isSelected ? '#5b8def' : '#3a4060'}
              strokeWidth={isSelected ? 2.5 : 1.5}
            />
            <text x={pos.x} y={pos.y + 1} textAnchor="middle" dominantBaseline="central"
              fill="#e8e8f0" fontSize="10" fontWeight="700">
              {pos.label}
            </text>
            <text x={pos.x} y={pos.y + 23} textAnchor="middle" fill="#6a6e88" fontSize="9">
              {p.pointCount} т.
            </text>
          </g>
        )
      })}
    </svg>
  )
}

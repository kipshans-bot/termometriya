export default function Legend() {
  return (
    <div className="legend">
      <div className="legend-item">
        <div className="legend-color" style={{ background: '#1a8a30' }} />
        Норма (&lt;28°C)
      </div>
      <div className="legend-item">
        <div className="legend-color" style={{ background: '#b89a20' }} />
        Предупреждение (28-35°C)
      </div>
      <div className="legend-item">
        <div className="legend-color" style={{ background: '#cc3030' }} />
        Авария (&ge;35°C)
      </div>
    </div>
  )
}

export function tempColor(temp: number, alpha = 1): string {
  const t = Math.max(0, Math.min(50, temp))
  if (t < 5) return `rgba(0, 100, 255, ${alpha})`
  if (t < 10) return `rgba(0, 200, 200, ${alpha})`
  if (t < 15) return `rgba(0, 235, 100, ${alpha})`
  if (t < 20) return `rgba(50, 240, 50, ${alpha})`
  if (t < 25) return `rgba(140, 220, 40, ${alpha})`
  if (t < 30) return `rgba(220, 200, 30, ${alpha})`
  if (t < 35) return `rgba(255, 150, 10, ${alpha})`
  if (t < 40) return `rgba(255, 80, 10, ${alpha})`
  if (t < 45) return `rgba(230, 30, 30, ${alpha})`
  return `rgba(180, 0, 50, ${alpha})`
}

export function tempColorHex(temp: number): string {
  const t = Math.max(0, Math.min(50, temp))
  if (t < 5) return '#0064ff'
  if (t < 10) return '#00c8c8'
  if (t < 15) return '#00eb64'
  if (t < 20) return '#32f032'
  if (t < 25) return '#8cdc28'
  if (t < 30) return '#dcc81e'
  if (t < 35) return '#ff960a'
  if (t < 40) return '#ff500a'
  if (t < 45) return '#e61e1e'
  return '#b40032'
}

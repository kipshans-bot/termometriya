export interface ElevatorLine {
  id: number
  name: string
  displayOrder: number
  silos: SiloSummary[]
}

export interface SiloSummary {
  id: number
  number: number
  fillLevel: number
  capacity: number
  cultureId: number
  cultureName: string
  pendantCount: number
  maxTemp: number
  avgTemp: number
  hasActiveAlert: boolean
  alertLevel: number
  lineId: number
  lineName: string
  grainLevelPointIndex?: number
}

export interface SiloDetail {
  id: number
  number: number
  lineId: number
  fillLevel: number
  capacity: number
  cultureId: number
  grainLevelPointIndex?: number
  lineName: string
  cultureName: string
  pendants: PendantInfo[]
  alerts: AlertInfo[]
}

export interface PendantInfo {
  id: number
  positionIndex: number
  pointCount: number
  displayOrder: number
  isCentral: boolean
  points: PointData[]
}

export interface PointData {
  index: number
  temp: number | null
  humidity: number | null
  isValid: boolean
}

export interface CultureInfo {
  id: number
  name: string
  normTemp: number
  warnTemp: number
  criticalTemp: number
  gradientWarn: number
  gradientCritical: number
  deviationThreshold: number
  highTempThreshold: number
  highTempGradient: number
  criticalHighTemp50: number
  soundEnabled: boolean
  emailEnabled: boolean
  emailRecipients: string
}

export const AlertType = {
  Normal: 0,
  Warning: 1,
  Critical: 2,
  GradientWarning: 3,
  GradientCritical: 4,
  DeviationWarning: 5,
  HumidityWarning: 6,
  SensorFault: 7,
} as const

export type AlertType = (typeof AlertType)[keyof typeof AlertType]

export interface AlertInfo {
  id: number
  siloId: number
  thermopendantId: number | null
  alertType: AlertType
  pointIndex: number
  value: number
  threshold: number
  message: string
  timestamp: string
  isActive: boolean
  acknowledgedAt: string | null
  resolvedAt: string | null
  siloNumber: number
  lineName: string
}

export interface HistoryPoint {
  thermopendantId: number
  pointIndex: number
  temperature: number
  humidity: number | null
  isValid: boolean
  timestamp: string
}

export interface AlertCounts {
  critical: number
  warning: number
  total: number
}

export interface SiloUpdate {
  siloId: number
  maxTemp: number
  avgTemp: number
  avgHumidity: number
  pointCount: number
  hasActiveAlert: boolean
  alertLevel: number
  pendants: PendantUpdate[]
}

export interface PendantUpdate {
  pendantId: number
  position: number
  maxTemp: number
  points: PointUpdate[]
}

export interface PointUpdate {
  index: number
  temp: number
  humidity: number | null
}

export interface PollingConfig {
  id: number
  normalIntervalSec: number
  elevatedIntervalSec: number
  currentMode: number
}

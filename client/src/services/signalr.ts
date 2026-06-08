import * as signalR from '@microsoft/signalr'

let connection: signalR.HubConnection | null = null
let connectionPromise: Promise<void> | null = null

function getConnection(): signalR.HubConnection {
  if (!connection) {
    connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/monitoring')
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .build()
  }
  return connection
}

export async function ensureConnected(): Promise<void> {
  if (connectionPromise) return connectionPromise
  const conn = getConnection()
  if (conn.state === signalR.HubConnectionState.Disconnected) {
    connectionPromise = conn.start()
    try {
      await connectionPromise
    } finally {
      connectionPromise = null
    }
  }
}

export function onSiloUpdate(handler: (data: unknown) => void): () => void {
  const conn = getConnection()
  conn.on('SiloUpdated', handler)
  ensureConnected().catch(() => {})
  return () => conn.off('SiloUpdated', handler)
}

export function onAlertTriggered(handler: (data: unknown) => void): () => void {
  const conn = getConnection()
  conn.on('AlertTriggered', handler)
  ensureConnected().catch(() => {})
  return () => conn.off('AlertTriggered', handler)
}

export function onAlertCounts(handler: (data: { critical: number; warning: number; total: number }) => void): () => void {
  const conn = getConnection()
  conn.on('AlertCounts', handler)
  ensureConnected().catch(() => {})
  return () => conn.off('AlertCounts', handler)
}

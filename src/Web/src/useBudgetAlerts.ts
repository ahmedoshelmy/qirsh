import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr'
import { useEffect, useState } from 'react'
import type { BudgetAlert } from './api'

/** Subscribes to the Budgets service's SignalR hub; returns whether the live connection is up. */
export function useBudgetAlerts(onAlert: (alert: BudgetAlert) => void): boolean {
  const [connected, setConnected] = useState(false)

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/alerts')
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()
    connection.on('budgetAlert', onAlert)
    connection.onreconnecting(() => setConnected(false))
    connection.onreconnected(() => setConnected(true))
    connection.onclose(() => setConnected(false))
    connection
      .start()
      .then(() => setConnected(connection.state === HubConnectionState.Connected))
      .catch(() => setConnected(false))
    return () => {
      void connection.stop()
    }
  }, [onAlert])

  return connected
}

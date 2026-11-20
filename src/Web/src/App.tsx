import { useQueryClient } from '@tanstack/react-query'
import { useCallback, useState } from 'react'
import type { BudgetAlert } from './api'
import { Budgets } from './Budgets'
import { alertText, monthKey } from './format'
import { PasteBox } from './PasteBox'
import { Transactions } from './Transactions'
import { useBudgetAlerts } from './useBudgetAlerts'

interface Toast {
  id: number
  text: string
  over: boolean
}

export function App() {
  const month = monthKey(new Date())
  const queryClient = useQueryClient()
  const [toasts, setToasts] = useState<Toast[]>([])

  const onAlert = useCallback(
    (a: BudgetAlert) => {
      const toast = { id: Date.now(), text: alertText(a.category, a.thresholdPercent, a.spent, a.limit, a.currency), over: a.thresholdPercent >= 100 }
      setToasts((current) => [...current, toast])
      setTimeout(() => setToasts((current) => current.filter((t) => t.id !== toast.id)), 8000)
      void queryClient.invalidateQueries({ queryKey: ['budgets'] })
    },
    [queryClient],
  )
  const live = useBudgetAlerts(onAlert)

  return (
    <>
      <header className="top">
        <div>
          <h1>Qirsh</h1>
          <p className="muted">Bank and wallet SMS → ledger → budgets</p>
        </div>
        <span className={`live ${live ? 'on' : ''}`} title="Live budget alerts (SignalR)">
          {live ? 'Live' : 'Offline'}
        </span>
      </header>
      <main className="grid">
        <PasteBox />
        <Budgets month={month} />
        <Transactions month={month} />
      </main>
      <div className="toasts" role="status" aria-live="polite">
        {toasts.map((t) => (
          <div key={t.id} className={`toast ${t.over ? 'over' : 'warn'}`}>
            {t.text}
          </div>
        ))}
      </div>
    </>
  )
}

import { useQuery } from '@tanstack/react-query'
import { api } from './api'
import { perCadence } from './format'

/** Subscriptions and bills the Ledger found: same merchant, steady rhythm, steady amount. */
export function Recurring() {
  const recurring = useQuery({ queryKey: ['recurring'], queryFn: api.recurring, refetchInterval: 30000 })

  return (
    <section className="card recurring">
      <h2>Recurring payments</h2>
      {recurring.data?.length === 0 && (
        <p className="muted">Nothing recurring yet. A merchant shows up here after three payments of a similar amount on a steady rhythm.</p>
      )}
      {recurring.error && <p className="error">{recurring.error.message}</p>}
      {!!recurring.data?.length && (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Merchant</th>
                <th className="num">Typical</th>
                <th>Seen</th>
                <th>Last paid</th>
                <th>Next expected</th>
              </tr>
            </thead>
            <tbody>
              {recurring.data.map((r) => (
                <tr key={`${r.merchant}-${r.currency}`}>
                  <td>{r.merchant}</td>
                  <td className="num">{perCadence(r.typicalAmount, r.cadence, r.currency)}</td>
                  <td>{r.occurrences}×</td>
                  <td>{r.lastPaidOn}</td>
                  <td className={r.isOverdue ? 'error' : undefined}>
                    {r.nextExpectedOn}
                    {r.isOverdue && ' · overdue'}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}

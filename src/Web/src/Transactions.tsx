import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from './api'
import { signedMoney } from './format'

export function Transactions({ month }: { month: string }) {
  const queryClient = useQueryClient()
  const transactions = useQuery({ queryKey: ['transactions', month], queryFn: () => api.transactions(month), refetchInterval: 5000 })
  const categories = useQuery({ queryKey: ['categories'], queryFn: api.categories })
  const move = useMutation({
    mutationFn: ({ id, category }: { id: string; category: string }) => api.recategorise(id, category),
    onSuccess: () => void queryClient.invalidateQueries(),
  })

  return (
    <section className="card transactions">
      <h2>Transactions</h2>
      <p className="muted">Change a category and Qirsh learns it: the next payment to that merchant is filed the same way.</p>
      {transactions.isError && <p className="error">Ledger unavailable.</p>}
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Date</th>
              <th>Merchant</th>
              <th>Account</th>
              <th className="num">Amount</th>
              <th>Category</th>
            </tr>
          </thead>
          <tbody>
            {transactions.data?.map((t) => (
              <tr key={t.id}>
                <td>{t.occurredOn}</td>
                <td dir="auto">{t.merchant ?? <span className="muted">{t.channel}</span>}</td>
                <td className="muted">{t.account}</td>
                <td className={`num ${t.direction === 'In' ? 'in' : 'out'}`}>{signedMoney(t.amount, t.direction, t.currency)}</td>
                <td>
                  <select
                    value={t.category}
                    onChange={(e) => move.mutate({ id: t.id, category: e.target.value })}
                    aria-label={`Category for ${t.merchant ?? 'transaction'}`}
                  >
                    {categories.data?.map((c) => (
                      <option key={c.name}>{c.name}</option>
                    ))}
                  </select>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {transactions.data?.length === 0 && <p className="muted">Nothing yet this month. Paste a message above.</p>}
      </div>
    </section>
  )
}

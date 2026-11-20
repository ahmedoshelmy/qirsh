import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { api } from './api'
import { barWidth, budgetTone, money } from './format'

export function Budgets({ month }: { month: string }) {
  const queryClient = useQueryClient()
  const budgets = useQuery({ queryKey: ['budgets', month], queryFn: () => api.budgets(month), refetchInterval: 5000 })
  const categories = useQuery({ queryKey: ['categories'], queryFn: api.categories })
  const [category, setCategory] = useState('')
  const [limit, setLimit] = useState('')
  const create = useMutation({
    mutationFn: () => api.createBudget(category, Number(limit), month),
    onSuccess: () => {
      setLimit('')
      void queryClient.invalidateQueries({ queryKey: ['budgets'] })
    },
  })

  const submit = (e: FormEvent) => {
    e.preventDefault()
    if (category && Number(limit) > 0) create.mutate()
  }

  const spendable = categories.data?.filter((c) => !c.isIncome) ?? []

  return (
    <section className="card budgets">
      <h2>Budgets · {month}</h2>
      {budgets.data?.length === 0 && <p className="muted">No budgets yet. Spending already recorded this month is counted when you add one.</p>}
      <ul className="budget-list">
        {budgets.data?.map((b) => (
          <li key={b.id}>
            <div className="row spread">
              <strong>{b.category}</strong>
              <span className="muted">
                {money(b.spent, b.currency)} / {money(b.limit, b.currency)}
              </span>
            </div>
            <div className="bar" aria-label={`${b.percentUsed}% used`}>
              <span className={budgetTone(b.percentUsed)} style={{ width: barWidth(b.percentUsed) }} />
            </div>
          </li>
        ))}
      </ul>
      <form className="row" onSubmit={submit}>
        <select value={category} onChange={(e) => setCategory(e.target.value)} aria-label="Category">
          <option value="">Category…</option>
          {spendable.map((c) => (
            <option key={c.name}>{c.name}</option>
          ))}
        </select>
        <input value={limit} onChange={(e) => setLimit(e.target.value)} inputMode="decimal" placeholder="Limit (EGP)" aria-label="Limit" />
        <button type="submit" disabled={create.isPending}>
          Add budget
        </button>
      </form>
      {create.error && <p className="error">{create.error.message}</p>}
    </section>
  )
}

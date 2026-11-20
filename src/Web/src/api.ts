// Typed calls to the three services. In compose nginx routes each path; in dev the Vite proxy does.

export interface TransactionRow {
  id: string
  account: string
  direction: 'Out' | 'In'
  amount: number
  currency: string
  merchant: string | null
  category: string
  channel: string
  occurredOn: string
  balanceAfter: number | null
}

export interface Category {
  name: string
  isIncome: boolean
}

export interface BudgetRow {
  id: string
  category: string
  month: string
  limit: number
  spent: number
  currency: string
  percentUsed: number
  alertedThreshold: number
}

export interface IntakeItem {
  index: number
  status: 'Recorded' | 'Duplicate' | 'Skipped'
  parsed: { ok: boolean; skip: string | null; bank: string | null; amount: number | null; currency: string; counterparty: string | null }
}

export interface IntakeResponse {
  recorded: number
  duplicates: number
  skipped: number
  items: IntakeItem[]
}

export interface BudgetAlert {
  budgetId: string
  category: string
  month: string
  thresholdPercent: number
  spent: number
  limit: number
  currency: string
}

async function request<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url, {
    ...init,
    headers: { 'Content-Type': 'application/json', ...init?.headers },
  })
  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as { title?: string; detail?: string } | null
    throw new Error(problem?.detail ?? problem?.title ?? `${response.status} ${response.statusText}`)
  }
  return response.status === 204 ? (undefined as T) : ((await response.json()) as T)
}

export const api = {
  transactions: (month: string) => request<TransactionRow[]>(`/api/transactions?month=${month}&take=200`),
  categories: () => request<Category[]>('/api/categories'),
  budgets: (month: string) => request<BudgetRow[]>(`/api/budgets?month=${month}`),
  pasteSms: (text: string) => request<IntakeResponse>('/api/sms', { method: 'POST', body: JSON.stringify({ text }) }),
  recategorise: (id: string, category: string) =>
    request<void>(`/api/transactions/${id}/category`, { method: 'PUT', body: JSON.stringify({ category }) }),
  createBudget: (category: string, limit: number, month: string) =>
    request<unknown>('/api/budgets', { method: 'POST', body: JSON.stringify({ category, limit, month }) }),
}

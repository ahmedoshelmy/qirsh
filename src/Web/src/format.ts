// Pure presentation helpers (unit-tested).

export function money(amount: number, currency = 'EGP'): string {
  const fixed = amount.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
  return `${currency} ${fixed}`
}

export function signedMoney(amount: number, direction: 'Out' | 'In', currency = 'EGP'): string {
  return `${direction === 'Out' ? '−' : '+'}${money(amount, currency)}`
}

/** yyyy-MM of a date, in local time. */
export function monthKey(date: Date): string {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}`
}

export type BudgetTone = 'ok' | 'warn' | 'over'

export function budgetTone(percentUsed: number): BudgetTone {
  if (percentUsed >= 100) return 'over'
  if (percentUsed >= 80) return 'warn'
  return 'ok'
}

/** Width of a progress bar, capped so an overspent budget still fits. */
export function barWidth(percentUsed: number): string {
  return `${Math.min(Math.max(percentUsed, 0), 100)}%`
}

export function alertText(category: string, threshold: number, spent: number, limit: number, currency: string): string {
  return threshold >= 100
    ? `${category} budget is used up: ${money(spent, currency)} of ${money(limit, currency)}`
    : `${category} is at ${threshold}% of its budget: ${money(spent, currency)} of ${money(limit, currency)}`
}

export function intakeSummary(recorded: number, duplicates: number, skipped: number): string {
  const parts = [`${recorded} recorded`]
  if (duplicates) parts.push(`${duplicates} already known`)
  if (skipped) parts.push(`${skipped} skipped (OTP, declined or no amount)`)
  return parts.join(' · ')
}

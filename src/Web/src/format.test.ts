import { describe, expect, it } from 'vitest'
import { alertText, barWidth, budgetTone, intakeSummary, money, monthKey, perCadence, signedMoney } from './format'

describe('format', () => {
  it('formats money with two decimals and grouping', () => {
    expect(money(1250)).toBe('EGP 1,250.00')
    expect(money(12.5, 'USD')).toBe('USD 12.50')
  })

  it('signs money by direction', () => {
    expect(signedMoney(10, 'Out')).toBe('−EGP 10.00')
    expect(signedMoney(10, 'In')).toBe('+EGP 10.00')
  })

  it('labels a recurring amount by its cadence', () => {
    expect(perCadence(100, 'Monthly')).toBe('EGP 100.00 / month')
    expect(perCadence(950, 'Yearly', 'USD')).toBe('USD 950.00 / year')
  })

  it('builds a month key', () => {
    expect(monthKey(new Date(2026, 0, 31))).toBe('2026-01')
  })

  it.each([
    [0, 'ok'],
    [79, 'ok'],
    [80, 'warn'],
    [100, 'over'],
    [150, 'over'],
  ] as const)('tone for %i%% is %s', (pct, tone) => {
    expect(budgetTone(pct)).toBe(tone)
  })

  it('caps bar width', () => {
    expect(barWidth(150)).toBe('100%')
    expect(barWidth(-3)).toBe('0%')
    expect(barWidth(42)).toBe('42%')
  })

  it('words alerts by threshold', () => {
    expect(alertText('Groceries', 80, 800, 1000, 'EGP')).toContain('at 80%')
    expect(alertText('Groceries', 100, 1100, 1000, 'EGP')).toContain('used up')
  })

  it('summarises an intake', () => {
    expect(intakeSummary(2, 0, 0)).toBe('2 recorded')
    expect(intakeSummary(1, 1, 2)).toBe('1 recorded · 1 already known · 2 skipped (OTP, declined or no amount)')
  })
})

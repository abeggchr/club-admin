import { describe, expect, it } from 'vitest'
import { formatChf, parseAmount, toSignedAmount, toTypedAmount } from './amount'

describe('parseAmount', () => {
  it.each([
    ['120', 120],
    ['1250.5', 1250.5],
    ['1250.50', 1250.5],
    ["1'250.50", 1250.5],
    ['1’250.50', 1250.5],
    ["1'234'567.89", 1234567.89],
    ['  12.30 ', 12.3],
    ['0.05', 0.05],
    ['-120', -120],
    ["-1'250.5", -1250.5],
  ])('accepts %s', (input, expected) => {
    expect(parseAmount(input)).toEqual({ ok: true, value: expected })
  })

  it.each([
    [''],
    ['   '],
    ['abc'],
    ['12a'],
    ['1.234'],
    ['12.345.6'],
    ["12'34"],
    ['1,5'],
    ['--5'],
    ['-'],
    ['.'],
  ])('rejects %j', (input) => {
    expect(parseAmount(input).ok).toBe(false)
  })

  it('explains the two-decimal rule', () => {
    expect(parseAmount('1.234')).toEqual({
      ok: false,
      error: 'Höchstens zwei Dezimalstellen erlaubt.',
    })
  })

  it('rejects zero', () => {
    expect(parseAmount('0.00').ok).toBe(false)
  })
})

describe('toSignedAmount', () => {
  it('stores an Ausgaben amount as negative', () => {
    expect(toSignedAmount(120, 'Expense')).toBe(-120)
  })

  it('stores an Einnahmen amount as positive', () => {
    expect(toSignedAmount(120, 'Income')).toBe(120)
  })

  it('flips the effect for a refund on an Ausgaben-Konto', () => {
    expect(toSignedAmount(-120, 'Expense')).toBe(120)
  })

  it('flips the effect for a reversal on an Einnahmen-Konto', () => {
    expect(toSignedAmount(-120, 'Income')).toBe(-120)
  })

  it('does not produce floating point noise', () => {
    expect(toSignedAmount(0.1 + 0.2, 'Expense')).toBe(-0.3)
  })
})

describe('formatChf', () => {
  it('uses Swiss thousand separators and two decimals', () => {
    expect(formatChf(1250.5)).toBe("1'250.50")
    expect(formatChf(7)).toBe('7.00')
    expect(formatChf(1234567.89)).toBe("1'234'567.89")
  })

  it('prefixes negative amounts with a minus', () => {
    expect(formatChf(-1250.5)).toBe("-1'250.50")
  })
})

describe('toTypedAmount', () => {
  it.each([
    [-120, 'Expense', 120],
    [50, 'Income', 50],
    [30, 'Expense', -30],
    [-30, 'Income', -30],
  ] as const)('shows stored %s of an %s Konto as %s', (stored, category, typed) => {
    expect(toTypedAmount(stored, category)).toBe(typed)
  })

  it('round-trips with toSignedAmount', () => {
    expect(toSignedAmount(toTypedAmount(-120.5, 'Expense'), 'Expense')).toBe(-120.5)
  })
})

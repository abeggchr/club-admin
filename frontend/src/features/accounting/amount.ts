import type { Category } from './types'

export type ParsedAmount = { ok: true; value: number } | { ok: false; error: string }

const PLAIN = /^(-?)(\d+)(?:\.(\d*))?$/
const GROUPED = /^(-?)(\d{1,3}(?:'\d{3})+)(?:\.(\d*))?$/

/**
 * Parses a user-typed CHF amount (e.g. "1'250.50", "1250.5", "-120").
 * The result is the typed number, not yet signed by Kategorie (see toSignedAmount).
 */
export function parseAmount(input: string): ParsedAmount {
  const text = input.trim().replace(/’/g, "'")
  if (text === '') return { ok: false, error: 'Betrag ist erforderlich.' }

  const match = GROUPED.exec(text) ?? PLAIN.exec(text)
  if (!match || match[3] === '') {
    return { ok: false, error: 'Ungültiger Betrag.' }
  }
  const [, sign, whole, decimals = ''] = match
  if (decimals.length > 2) return { ok: false, error: 'Höchstens zwei Dezimalstellen erlaubt.' }

  const cents = Number(whole.replaceAll("'", '')) * 100 + Number(decimals.padEnd(2, '0'))
  if (cents === 0) return { ok: false, error: 'Betrag darf nicht 0 sein.' }
  return { ok: true, value: (sign === '-' ? -cents : cents) / 100 }
}

/**
 * Converts the amount typed in the Konto's natural direction into the stored amount,
 * signed from the bank's point of view: Ausgaben negative, Einnahmen positive.
 * A negative input is a refund or reversal and flips the effect.
 */
export function toSignedAmount(typed: number, category: Category): number {
  const cents = Math.round(typed * 100)
  return (category === 'Expense' ? -cents : cents) / 100 || 0
}

/** Inverse of toSignedAmount: the amount as the user types it in the Konto's natural direction. */
export function toTypedAmount(stored: number, category: Category): number {
  const cents = Math.round(stored * 100)
  return (category === 'Expense' ? -cents : cents) / 100 || 0
}

/** Formats an amount with Swiss thousand separators and exactly two decimals, e.g. 1'250.50. */
export function formatChf(amount: number): string {
  const cents = Math.round(Math.abs(amount) * 100)
  const whole = Math.floor(cents / 100)
    .toString()
    .replace(/\B(?=(\d{3})+(?!\d))/g, "'")
  const fraction = String(cents % 100).padStart(2, '0')
  return `${amount < 0 && cents > 0 ? '-' : ''}${whole}.${fraction}`
}

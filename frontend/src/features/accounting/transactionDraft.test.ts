import { describe, expect, it } from 'vitest'
import {
  remainder,
  draftFromTransaction,
  emptyDraft,
  entryAmount,
  isDraftDirty,
  toTransactionInput,
  validateDraft,
  type TransactionDraft,
} from './transactionDraft'
import type { Account } from './types'

const accounts: Account[] = [
  { number: 3400, name: 'Jahresbeiträge', category: 'Income', active: true },
  { number: 6010, name: 'Miete', category: 'Expense', active: true },
]

const empty = emptyDraft('2026-11-08')

function filled(overrides: Partial<TransactionDraft> = {}): TransactionDraft {
  return {
    date: '2026-11-08',
    amount: '-120',
    entries: [{ id: null, text: 'Miete', accountNumber: 6010, amount: null }],
    ...overrides,
  }
}

describe('entryAmount (first-row pre-fill)', () => {
  it('follows the Bewegung Betrag in the Konto’s natural direction until edited', () => {
    expect(entryAmount(filled(), 0, accounts)).toBe('120.00') // Ausgabe: -120 typed as 120
    const income = filled({
      amount: '50',
      entries: [{ id: null, text: 'x', accountNumber: 3400, amount: null }],
    })
    expect(entryAmount(income, 0, accounts)).toBe('50.00')
  })

  it('shows the Bewegung Betrag as typed while no Konto is chosen or it is unparsable', () => {
    const draft = filled({ entries: [{ id: null, text: '', accountNumber: null, amount: null }] })
    expect(entryAmount(draft, 0, accounts)).toBe('-120')
    expect(entryAmount({ ...draft, amount: '12.' }, 0, accounts)).toBe('12.')
  })

  it('stops following once the user edited the Buchung Betrag', () => {
    const draft = filled()
    draft.entries[0].amount = '100'
    expect(entryAmount({ ...draft, amount: '-200' }, 0, accounts)).toBe('100')
  })

  it('does not pre-fill further rows', () => {
    const draft = filled()
    draft.entries.push({ id: null, text: '', accountNumber: null, amount: null })
    expect(entryAmount(draft, 1, accounts)).toBe('')
  })
})

describe('remainder', () => {
  it('is zero when the Buchung matches the Bewegung', () => {
    expect(remainder(filled(), accounts)).toBe(0)
  })

  it('signs typed amounts per Kategorie', () => {
    const draft = filled({ amount: '-100' })
    draft.entries[0].amount = '30' // Ausgabe 30 => -30
    expect(remainder(draft, accounts)).toBe(-70)
    draft.entries.push({ id: null, text: 'Spende', accountNumber: 3400, amount: '20' }) // Einnahme 20 => +20
    expect(remainder(draft, accounts)).toBe(-90)
  })

  it('treats a negative typed amount as a reversal', () => {
    const draft = filled({ amount: '100' })
    draft.entries[0].amount = '-100' // Ausgabe -100 => +100
    expect(remainder(draft, accounts)).toBe(0)
  })

  it('is cents-exact', () => {
    const draft = filled({ amount: '0.30' })
    draft.entries = [
      { id: null, text: 'a', accountNumber: 3400, amount: '0.10' },
      { id: null, text: 'b', accountNumber: 3400, amount: '0.20' },
    ]
    expect(remainder(draft, accounts)).toBe(0)
  })

  it('is null while the Bewegung Betrag is not a valid amount', () => {
    expect(remainder(filled({ amount: '' }), accounts)).toBeNull()
  })
})

describe('isDraftDirty', () => {
  it('is clean when nothing changed', () => {
    expect(isDraftDirty(empty, structuredClone(empty), accounts)).toBe(false)
  })

  it.each([
    ['date', (d: TransactionDraft) => ({ ...d, date: '2026-11-09' })],
    ['amount', (d: TransactionDraft) => ({ ...d, amount: '5' })],
    ['text', (d: TransactionDraft) => ({ ...d, entries: [{ ...d.entries[0], text: 'x' }] })],
    ['account', (d: TransactionDraft) => ({ ...d, entries: [{ ...d.entries[0], accountNumber: 6010 }] })],
    ['Buchung amount', (d: TransactionDraft) => ({ ...d, entries: [{ ...d.entries[0], amount: '5' }] })],
  ])('is dirty when the %s changed', (_field, change) => {
    expect(isDraftDirty(empty, change(empty), accounts)).toBe(true)
  })

  it('is clean again when a change is reverted', () => {
    const edited = { ...empty, amount: '5' }
    expect(isDraftDirty(empty, { ...edited, amount: '' }, accounts)).toBe(false)
  })

  it('is clean for a loaded Bewegung until something is changed', () => {
    const loaded = draftFromTransaction(
      { date: '2026-03-01', amount: -120 },
      [{ id: 7, date: '2026-03-01', text: 'Miete', accountNumber: 6010, amount: -120 }],
      accounts,
    )
    expect(loaded.amount).toBe('-120.00')
    expect(loaded.entries[0]).toEqual({ id: 7, text: 'Miete', accountNumber: 6010, amount: '120.00' })
    expect(isDraftDirty(loaded, structuredClone(loaded), accounts)).toBe(false)
  })
})

describe('validateDraft / can-save', () => {
  it('accepts a balanced draft', () => {
    expect(validateDraft(filled(), accounts, '2026-01-01')).toBeNull()
  })

  it('reports missing fields', () => {
    const errors = validateDraft(empty, accounts, '2026-01-01')
    expect(errors?.amount).toBe('Betrag ist erforderlich.')
    expect(errors?.entries[0]).toMatchObject({ text: 'Text ist erforderlich.', account: 'Konto ist erforderlich.' })
  })

  it('rejects a date before the Anfangssaldo', () => {
    expect(validateDraft(filled({ date: '2025-12-31' }), accounts, '2026-01-01')?.date).toContain('vor dem Anfangssaldo')
  })

  it('rejects a Differenz other than zero', () => {
    const draft = filled()
    draft.entries[0].amount = '100'
    expect(validateDraft(draft, accounts, '2026-01-01')?.remainder).toBe('Die Differenz von -20.00 muss 0 sein.')
  })

  it('needs at least one Buchung', () => {
    expect(validateDraft(filled({ entries: [] }), accounts, '2026-01-01')?.remainder).toBeDefined()
  })
})

describe('toTransactionInput', () => {
  it('sends signed, trimmed amounts and Buchung ids', () => {
    const draft = filled()
    draft.entries[0] = { id: 7, text: ' Miete ', accountNumber: 6010, amount: null }
    expect(toTransactionInput(draft, accounts)).toEqual({
      date: '2026-11-08',
      amount: -120,
      entries: [{ id: 7, text: 'Miete', accountNumber: 6010, amount: -120 }],
    })
  })
})

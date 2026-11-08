import { formatChf, parseAmount, toSignedAmount, toTypedAmount } from './amount'
import { entryDateError } from './date'
import type { Account, Entry, TransactionInput } from './types'

/** One Buchung row of the form. `amount: null` means the row still follows the Bewegung Betrag. */
export type EntryRow = {
  id: number | null
  text: string
  accountNumber: number | null
  /** Typed in the Konto's natural direction (Ausgaben positive for an expense). */
  amount: string | null
}

/** The Bewegung form's field values as the user sees them. `amount` is the typed, bank-signed Bewegung Betrag. */
export type TransactionDraft = {
  date: string
  amount: string
  entries: EntryRow[]
}

export type DraftErrors = {
  date?: string
  amount?: string
  entries: { text?: string; account?: string; amount?: string }[]
  remainder?: string
}

export function emptyDraft(date: string): TransactionDraft {
  return { date, amount: '', entries: [{ id: null, text: '', accountNumber: null, amount: null }] }
}

export function draftFromTransaction(
  transaction: { date: string; amount: number },
  entries: Entry[],
  accounts: Account[],
): TransactionDraft {
  return {
    date: transaction.date,
    amount: formatChf(transaction.amount),
    entries: entries.map((e) => {
      const account = accounts.find((a) => a.number === e.accountNumber)
      return {
        id: e.id,
        text: e.text,
        accountNumber: e.accountNumber,
        amount: formatChf(account ? toTypedAmount(e.amount, account.category) : e.amount),
      }
    }),
  }
}

function categoryOf(accountNumber: number | null, accounts: Account[]) {
  return accounts.find((a) => a.number === accountNumber)?.category
}

/** The Buchung Betrag shown in a row: what the user typed, or for the first untouched row the Bewegung Betrag. */
export function entryAmount(draft: TransactionDraft, index: number, accounts: Account[]): string {
  const row = draft.entries[index]
  if (row.amount !== null) return row.amount
  if (index !== 0) return ''
  const parsed = parseAmount(draft.amount)
  const category = categoryOf(row.accountNumber, accounts)
  if (!parsed.ok || !category) return draft.amount
  return formatChf(toTypedAmount(parsed.value, category))
}

function toCents(amount: number): number {
  return Math.round(amount * 100)
}

/** The signed amount of a row, or null when its amount or Konto is not (yet) valid. */
function signedEntryAmount(draft: TransactionDraft, index: number, accounts: Account[]): number | null {
  const category = categoryOf(draft.entries[index].accountNumber, accounts)
  const parsed = parseAmount(entryAmount(draft, index, accounts))
  return category && parsed.ok ? toSignedAmount(parsed.value, category) : null
}

/**
 * Bewegung Betrag minus the signed sum of the Buchungen; 0 means the Bewegung is fully booked.
 * Rows that are not valid yet count as 0. Null while the Bewegung Betrag is not a valid amount.
 */
export function remainder(draft: TransactionDraft, accounts: Account[]): number | null {
  const total = parseAmount(draft.amount)
  if (!total.ok) return null
  const booked = draft.entries.reduce((sum, _row, i) => sum + toCents(signedEntryAmount(draft, i, accounts) ?? 0), 0)
  return (toCents(total.value) - booked) / 100
}

/** True when the user has changed anything away from where the form started. */
export function isDraftDirty(initial: TransactionDraft, current: TransactionDraft, accounts: Account[]): boolean {
  if (initial.date !== current.date || initial.amount !== current.amount) return true
  if (initial.entries.length !== current.entries.length) return true
  return current.entries.some((row, i) => {
    const before = initial.entries[i]
    return (
      before.id !== row.id ||
      before.text !== row.text ||
      before.accountNumber !== row.accountNumber ||
      entryAmount(initial, i, accounts) !== entryAmount(current, i, accounts)
    )
  })
}

/** The form's validation errors, or null when the draft can be saved. */
export function validateDraft(
  draft: TransactionDraft,
  accounts: Account[],
  openingStartDate: string | undefined,
): DraftErrors | null {
  const errors: DraftErrors = { entries: [] }
  let any = false

  const dateError = entryDateError(draft.date, openingStartDate)
  if (dateError) (errors.date = dateError), (any = true)
  const total = parseAmount(draft.amount)
  if (!total.ok) (errors.amount = total.error), (any = true)

  draft.entries.forEach((row, i) => {
    const rowErrors: DraftErrors['entries'][number] = {}
    if (row.text.trim() === '') rowErrors.text = 'Text ist erforderlich.'
    if (!categoryOf(row.accountNumber, accounts)) rowErrors.account = 'Konto ist erforderlich.'
    const parsed = parseAmount(entryAmount(draft, i, accounts))
    if (!parsed.ok) rowErrors.amount = parsed.error
    if (Object.keys(rowErrors).length > 0) any = true
    errors.entries.push(rowErrors)
  })

  if (draft.entries.length === 0) {
    errors.remainder = 'Eine Bewegung braucht mindestens eine Buchung.'
    any = true
  } else if (total.ok && !errors.entries.some((e) => e.account || e.amount)) {
    const rest = remainder(draft, accounts)
    if (rest !== 0 && rest !== null) {
      errors.remainder = `Die Differenz von ${formatChf(rest)} muss 0 sein.`
      any = true
    }
  }
  return any ? errors : null
}

/** The request body for a draft that passes validateDraft; null otherwise. */
export function toTransactionInput(draft: TransactionDraft, accounts: Account[]): TransactionInput | null {
  const total = parseAmount(draft.amount)
  if (!total.ok) return null
  const entries: TransactionInput['entries'] = []
  for (let i = 0; i < draft.entries.length; i++) {
    const amount = signedEntryAmount(draft, i, accounts)
    if (amount === null) return null
    const row = draft.entries[i]
    entries.push({ id: row.id, text: row.text.trim(), accountNumber: row.accountNumber!, amount })
  }
  return { date: draft.date, amount: total.value, entries }
}

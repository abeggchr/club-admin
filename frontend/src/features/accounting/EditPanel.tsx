import { useEffect, useState, type FormEvent } from 'react'
import { useQueryClient, useSuspenseQuery } from '@tanstack/react-query'
import {
  accountsQueryOptions,
  createEntry,
  deleteEntry,
  invalidateJournalQueries,
  openingBalanceQueryOptions,
  updateEntry,
} from './api'
import { entryDateError } from './date'
import { formatChf, parseAmount, toSignedAmount, toTypedAmount } from './amount'
import { isEntryDraftDirty, type EntryDraft } from './entryDraft'
import { KontoPicker } from './KontoPicker'
import type { Entry } from './types'

function today(): string {
  const d = new Date()
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

const input = 'rounded border border-slate-300 p-2'

type Errors = {
  date?: string
  text?: string
  account?: string
  amount?: string
  server?: string
}

type Props = {
  /** When set, the form edits this Buchung instead of creating a new one. */
  entry?: Entry
  /** Closes the form. Offered as "Abbrechen"; after saving an edit it is called automatically. */
  onDone?: () => void
  /** Reports whether the form holds unsaved changes. */
  onDirtyChange?: (dirty: boolean) => void
}

export function EditPanel({ entry, onDone, onDirtyChange }: Props = {}) {
  const queryClient = useQueryClient()
  const { data: accounts } = useSuspenseQuery(accountsQueryOptions)
  const { data: openingBalance } = useSuspenseQuery(openingBalanceQueryOptions)
  const existing = entry ? accounts.find((a) => a.number === entry.accountNumber) : undefined
  const [date, setDate] = useState(entry?.date ?? today())
  const [text, setText] = useState(entry?.text ?? '')
  const [accountNumber, setAccountNumber] = useState<number | null>(entry?.accountNumber ?? null)
  const [amount, setAmount] = useState(
    entry && existing ? formatChf(toTypedAmount(entry.amount, existing.category)) : '',
  )
  const [errors, setErrors] = useState<Errors>({})
  const [baseline, setBaseline] = useState<EntryDraft>({ date, text, accountNumber, amount })
  const dirty = isEntryDraftDirty(baseline, { date, text, accountNumber, amount })

  useEffect(() => {
    onDirtyChange?.(dirty)
    return () => onDirtyChange?.(false)
  }, [dirty, onDirtyChange])

  async function onDelete() {
    if (!entry || !window.confirm('Diese Buchung wirklich löschen?')) return
    try {
      await deleteEntry(entry.id)
      await invalidateJournalQueries(queryClient)
      onDone?.()
    } catch (err) {
      setErrors({ server: err instanceof Error ? err.message : 'Löschen fehlgeschlagen.' })
    }
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    const next: Errors = {}
    const account = accounts.find((a) => a.number === accountNumber)
    const parsed = parseAmount(amount)
    const dateError = entryDateError(date, openingBalance?.startDate)
    if (dateError) next.date = dateError
    if (text.trim() === '') next.text = 'Text ist erforderlich.'
    if (!account) next.account = 'Konto ist erforderlich.'
    if (!parsed.ok) next.amount = parsed.error
    setErrors(next)
    if (next.date || next.text || next.account || next.amount || !account || !parsed.ok) return

    try {
      const body = {
        date,
        text: text.trim(),
        accountNumber: account.number,
        amount: toSignedAmount(parsed.value, account.category),
      }
      if (entry) await updateEntry(entry.id, body)
      else await createEntry(body)
      await invalidateJournalQueries(queryClient)
      if (entry) {
        onDone?.()
      } else {
        setText('')
        setAmount('')
        setBaseline({ date, text: '', accountNumber, amount: '' })
      }
    } catch (err) {
      setErrors({
        server: err instanceof Error ? err.message : 'Speichern fehlgeschlagen.',
      })
    }
  }

  return (
    <form onSubmit={onSubmit} noValidate className="flex flex-col gap-3">
      <h3 className="font-medium">{entry ? 'Buchung bearbeiten' : 'Neue Buchung'}</h3>
      <label className="flex flex-col text-sm">
        Datum
        <input
          type="date"
          value={date}
          onChange={(e) => setDate(e.target.value)}
          className={input}
        />
        {errors.date && <span className="text-red-600">{errors.date}</span>}
      </label>
      <label className="flex flex-col text-sm">
        Text
        <input value={text} onChange={(e) => setText(e.target.value)} className={input} />
        {errors.text && <span className="text-red-600">{errors.text}</span>}
      </label>
      <label className="flex flex-col text-sm">
        Konto
        <KontoPicker value={accountNumber} onChange={setAccountNumber} className={input} />
        {errors.account && <span className="text-red-600">{errors.account}</span>}
      </label>
      <label className="flex flex-col text-sm">
        Betrag (CHF)
        <input
          inputMode="decimal"
          value={amount}
          onChange={(e) => setAmount(e.target.value)}
          className={`${input} text-right`}
        />
        {errors.amount && <span className="text-red-600">{errors.amount}</span>}
      </label>
      <div className="flex justify-end gap-2">
        {entry && (
          <button type="button" onClick={onDelete} className="rounded border border-slate-300 text-red-700 hover:bg-red-100 px-4 py-2">
            Löschen
          </button>
        )}
        {onDone && (
          <button type="button" onClick={onDone} className="rounded border border-slate-300 hover:bg-sky-100 px-4 py-2">
            Abbrechen
          </button>
        )}
        <button type="submit" className="rounded bg-sky-600 px-4 py-2 text-white hover:bg-sky-700">
          {entry ? 'Speichern' : 'Buchen'}
        </button>
      </div>
      {errors.server && <p className="text-red-600">{errors.server}</p>}
    </form>
  )
}

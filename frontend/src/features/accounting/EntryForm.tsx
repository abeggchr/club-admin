import { useState, type FormEvent } from 'react'
import { useQueryClient, useSuspenseQuery } from '@tanstack/react-query'
import { accountsQueryOptions, createEntry, entriesQueryOptions } from './api'
import { parseAmount, toSignedAmount } from './amount'
import { KontoPicker } from './KontoPicker'

function today(): string {
  const d = new Date()
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

const input = 'rounded border border-slate-300 p-2'

type Errors = { text?: string; account?: string; amount?: string; server?: string }

export function EntryForm() {
  const queryClient = useQueryClient()
  const { data: accounts } = useSuspenseQuery(accountsQueryOptions)
  const [date, setDate] = useState(today())
  const [text, setText] = useState('')
  const [accountNumber, setAccountNumber] = useState<number | null>(null)
  const [amount, setAmount] = useState('')
  const [errors, setErrors] = useState<Errors>({})

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    const next: Errors = {}
    const account = accounts.find((a) => a.number === accountNumber)
    const parsed = parseAmount(amount)
    if (text.trim() === '') next.text = 'Text ist erforderlich.'
    if (!account) next.account = 'Konto ist erforderlich.'
    if (!parsed.ok) next.amount = parsed.error
    setErrors(next)
    if (next.text || next.account || next.amount || !account || !parsed.ok) return

    try {
      await createEntry({
        date,
        text: text.trim(),
        accountNumber: account.number,
        amount: toSignedAmount(parsed.value, account.category),
      })
      await queryClient.invalidateQueries({ queryKey: entriesQueryOptions.queryKey })
      setText('')
      setAmount('')
    } catch (err) {
      setErrors({ server: err instanceof Error ? err.message : 'Speichern fehlgeschlagen.' })
    }
  }

  return (
    <form onSubmit={onSubmit} noValidate className="mt-4 flex flex-wrap items-start gap-3">
      <label className="flex flex-col text-sm">
        Datum
        <input type="date" required value={date} onChange={(e) => setDate(e.target.value)} className={input} />
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
      <button type="submit" className="mt-5 rounded bg-sky-600 px-4 py-2 text-white hover:bg-sky-700">
        Buchen
      </button>
      {errors.server && <p className="w-full text-red-600">{errors.server}</p>}
    </form>
  )
}

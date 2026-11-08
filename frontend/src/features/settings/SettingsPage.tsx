import { useState, type FormEvent } from 'react'
import { useQueryClient, useSuspenseQuery } from '@tanstack/react-query'
import {
  invalidateJournalQueries,
  openingBalanceQueryOptions,
  saveOpeningBalance,
} from '../accounting/api'
import { formatChf, parseAmount } from '../accounting/amount'

export function SettingsPage() {
  const queryClient = useQueryClient()
  const { data: openingBalance } = useSuspenseQuery(openingBalanceQueryOptions)
  const [amount, setAmount] = useState(openingBalance ? formatChf(openingBalance.openingBalance) : '')
  const [startDate, setStartDate] = useState(openingBalance?.startDate ?? '')
  const [error, setError] = useState<string | null>(null)
  const [saved, setSaved] = useState(false)

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    setError(null)
    setSaved(false)
    const parsed = parseAmount(amount, { allowZero: true })
    if (!parsed.ok) {
      setError(parsed.error)
      return
    }
    try {
      const result = await saveOpeningBalance({ openingBalance: parsed.value, startDate })
      queryClient.setQueryData(openingBalanceQueryOptions.queryKey, result)
      await invalidateJournalQueries(queryClient)
      setAmount(formatChf(result.openingBalance))
      setSaved(true)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Speichern fehlgeschlagen.')
    }
  }

  return (
    <div className="px-8">
      <h2 className="mt-4 text-xl font-semibold text-sky-900">Einstellungen</h2>
      <form onSubmit={onSubmit} noValidate className="mt-6 flex max-w-sm flex-col gap-3">
        <h3 className="font-medium">Anfangssaldo</h3>
        <label className="flex flex-col text-sm">
          Betrag
          <input
            inputMode="decimal"
            value={amount}
            onChange={(e) => setAmount(e.target.value)}
            className="rounded border border-slate-300 p-2"
          />
        </label>
        <label className="flex flex-col text-sm">
          Startdatum
          <input
            type="date"
            required
            value={startDate}
            onChange={(e) => setStartDate(e.target.value)}
            className="rounded border border-slate-300 p-2"
          />
        </label>
        <button type="submit" className="self-start rounded bg-sky-600 px-4 py-2 text-white hover:bg-sky-700">
          Speichern
        </button>
        {saved && <p className="text-green-700">Gespeichert.</p>}
        {error && <p className="text-red-600">{error}</p>}
      </form>
    </div>
  )
}

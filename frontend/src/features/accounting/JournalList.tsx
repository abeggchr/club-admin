import { useState } from 'react'
import { keepPreviousData, useQuery, useQueryClient, useSuspenseQuery } from '@tanstack/react-query'
import {
  accountsQueryOptions,
  deleteEntry,
  entriesQueryOptions,
  filteredEntriesQueryOptions,
  noFilters,
  type JournalFilters,
} from './api'
import { EntryForm } from './EntryForm'
import { formatChf } from './amount'
import { accountLabel } from './KontoPicker'

export function JournalList() {
  const queryClient = useQueryClient()
  const [editingId, setEditingId] = useState<number | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [filters, setFilters] = useState<JournalFilters>(noFilters)
  const { data: entries = [], isError } = useQuery({
    ...filteredEntriesQueryOptions(filters),
    placeholderData: keepPreviousData,
  })
  const { data: accounts } = useSuspenseQuery(accountsQueryOptions)
  const byNumber = new Map(accounts.map((a) => [a.number, a]))

  const filtered = Object.values(filters).some((v) => v.trim() !== '')
  const set = (patch: Partial<JournalFilters>) => setFilters({ ...filters, ...patch })
  const input = 'rounded border border-slate-300 p-1'

  async function onDelete(id: number) {
    if (!window.confirm('Diese Buchung wirklich löschen?')) return
    try {
      await deleteEntry(id)
      setError(null)
      await queryClient.invalidateQueries({
        queryKey: entriesQueryOptions.queryKey,
      })
      await queryClient.invalidateQueries({ queryKey: ['expected-balance'] })
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Löschen fehlgeschlagen.')
    }
  }

  return (
    <>
      <div className="mt-6 flex flex-wrap items-end gap-3 text-sm">
        <label className="flex flex-col">
          Von
          <input type="date" value={filters.from} onChange={(e) => set({ from: e.target.value })} className={input} />
        </label>
        <label className="flex flex-col">
          Bis
          <input type="date" value={filters.to} onChange={(e) => set({ to: e.target.value })} className={input} />
        </label>
        <label className="flex flex-col">
          Konto
          <select value={filters.account} onChange={(e) => set({ account: e.target.value })} className={input}>
            <option value="">Alle Konten</option>
            {[
              { label: 'Einnahmen', category: 'Income' },
              { label: 'Ausgaben', category: 'Expense' },
            ].map((g) => (
              <optgroup key={g.label} label={g.label}>
                {accounts
                  .filter((a) => a.category === g.category)
                  .map((a) => (
                    <option key={a.number} value={a.number}>
                      {accountLabel(a)}
                      {a.active ? '' : ' (inaktiv)'}
                    </option>
                  ))}
              </optgroup>
            ))}
          </select>
        </label>
        <label className="flex flex-col">
          Suche im Text
          <input type="search" value={filters.q} onChange={(e) => set({ q: e.target.value })} className={input} />
        </label>
        <button
          type="button"
          disabled={!filtered}
          onClick={() => setFilters(noFilters)}
          className="rounded border border-slate-300 px-3 py-1 enabled:hover:bg-slate-100 disabled:opacity-50"
        >
          Filter zurücksetzen
        </button>
      </div>
      {isError && <p className="mt-4 text-red-600">Buchungen konnten nicht geladen werden.</p>}
      {entries.length === 0 && (
        <p className="mt-6 text-slate-500">{filtered ? 'Keine Buchungen gefunden.' : 'Noch keine Buchungen.'}</p>
      )}
      {error && <p className="mt-4 text-red-600">{error}</p>}
      {entries.length > 0 && (
      <table className="mt-6 w-full max-w-4xl text-sm">
        <thead>
          <tr className="border-b border-slate-300 text-left">
            <th className="py-1">Datum</th>
            <th>Text</th>
            <th>Konto</th>
            <th className="text-right">Betrag</th>
            <th className="text-right">Kontostand</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {entries.map((e) => {
            const account = byNumber.get(e.accountNumber)
            if (e.id === editingId) {
              return (
                <tr key={e.id} className="border-b border-slate-100">
                  <td colSpan={6}>
                    <EntryForm entry={e} onDone={() => setEditingId(null)} />
                  </td>
                </tr>
              )
            }
            return (
              <tr key={e.id} className="border-b border-slate-100">
                <td className="py-1">{new Date(e.date).toLocaleDateString('de-CH')}</td>
                <td>{e.text}</td>
                <td>{account ? accountLabel(account) : e.accountNumber}</td>
                <td className={`text-right tabular-nums ${e.amount < 0 ? 'text-red-700' : ''}`}>
                  {formatChf(e.amount)}
                </td>
                <td className="text-right tabular-nums">{formatChf(e.balance)}</td>
                <td className="space-x-2 pl-2 text-right">
                  <button
                    type="button"
                    onClick={() => setEditingId(e.id)}
                    className="text-sky-700 hover:underline"
                  >
                    Bearbeiten
                  </button>
                  <button
                    type="button"
                    onClick={() => onDelete(e.id)}
                    className="text-red-700 hover:underline"
                  >
                    Löschen
                  </button>
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>
      )}
    </>
  )
}

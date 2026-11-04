import { useState } from 'react'
import { useQueryClient, useSuspenseQuery } from '@tanstack/react-query'
import { accountsQueryOptions, deleteEntry, entriesQueryOptions } from './api'
import { EntryForm } from './EntryForm'
import { formatChf } from './amount'
import { accountLabel } from './KontoPicker'

export function JournalList() {
  const queryClient = useQueryClient()
  const [editingId, setEditingId] = useState<number | null>(null)
  const [error, setError] = useState<string | null>(null)
  const { data: entries } = useSuspenseQuery(entriesQueryOptions)
  const { data: accounts } = useSuspenseQuery(accountsQueryOptions)
  const byNumber = new Map(accounts.map((a) => [a.number, a]))

  if (entries.length === 0) return <p className="mt-6 text-slate-500">Noch keine Buchungen.</p>

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
      {error && <p className="mt-4 text-red-600">{error}</p>}
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
    </>
  )
}

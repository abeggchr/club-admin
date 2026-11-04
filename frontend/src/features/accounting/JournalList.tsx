import { useSuspenseQuery } from '@tanstack/react-query'
import { accountsQueryOptions, entriesQueryOptions } from './api'
import { formatChf } from './amount'
import { accountLabel } from './KontoPicker'

export function JournalList() {
  const { data: entries } = useSuspenseQuery(entriesQueryOptions)
  const { data: accounts } = useSuspenseQuery(accountsQueryOptions)
  const byNumber = new Map(accounts.map((a) => [a.number, a]))

  if (entries.length === 0) return <p className="mt-6 text-slate-500">Noch keine Buchungen.</p>

  return (
    <table className="mt-6 w-full max-w-4xl text-sm">
      <thead>
        <tr className="border-b border-slate-300 text-left">
          <th className="py-1">Datum</th>
          <th>Text</th>
          <th>Konto</th>
          <th className="text-right">Betrag</th>
          <th className="text-right">Kontostand</th>
        </tr>
      </thead>
      <tbody>
        {entries.map((e) => {
          const account = byNumber.get(e.accountNumber)
          return (
            <tr key={e.id} className="border-b border-slate-100">
              <td className="py-1">{new Date(e.date).toLocaleDateString('de-CH')}</td>
              <td>{e.text}</td>
              <td>{account ? accountLabel(account) : e.accountNumber}</td>
              <td className={`text-right tabular-nums ${e.amount < 0 ? 'text-red-700' : ''}`}>
                {formatChf(e.amount)}
              </td>
              <td className="text-right tabular-nums">{formatChf(e.balance)}</td>
            </tr>
          )
        })}
      </tbody>
    </table>
  )
}

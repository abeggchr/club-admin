import { MdArrowDownward, MdArrowUpward } from 'react-icons/md'
import { formatChf } from './amount'
import type { Transaction } from './types'

type Props = {
  transactions: Transaction[]
  selectedId: number | null
  onSelect: (id: number) => void
}

/** The Bewegungen, newest first, each with the Kontostand after it. */
export function TransactionsTable({ transactions, selectedId, onSelect }: Props) {
  return (
    <div className="min-w-0 flex-1 overflow-x-auto rounded border border-slate-200">
      <table className="w-full text-left text-sm">
        <thead className="border-b border-slate-200 bg-slate-50">
          <tr>
            <th className="px-3 py-2 font-medium">Datum</th>
            <th className="px-3 py-2 font-medium">Art</th>
            <th className="px-3 py-2 text-right font-medium">Betrag</th>
            <th className="px-3 py-2 text-right font-medium">Kontostand</th>
          </tr>
        </thead>
        <tbody>
          {transactions.length === 0 ? (
            <tr>
              <td colSpan={4} className="px-3 py-8 text-center text-slate-500">
                Noch keine Bewegungen.
              </td>
            </tr>
          ) : (
            transactions.map((t) => {
              const selected = t.id === selectedId
              const label = t.amount < 0 ? 'Auszahlung' : 'Einzahlung'
              const Icon = t.amount < 0 ? MdArrowUpward : MdArrowDownward
              return (
                <tr
                  key={t.id}
                  onClick={() => onSelect(t.id)}
                  aria-selected={selected}
                  className={`cursor-pointer border-b border-slate-100 last:border-0 ${
                    selected ? 'bg-sky-50 hover:bg-sky-100' : 'hover:bg-slate-50'
                  }`}
                >
                  <td className="px-3 py-2">{new Date(t.date).toLocaleDateString('de-CH')}</td>
                  <td className="px-3 py-2">
                    <Icon
                      role="img"
                      aria-label={label}
                      title={label}
                      className={`size-5 ${t.amount < 0 ? 'text-red-700' : 'text-green-700'}`}
                    />
                  </td>
                  <td className={`px-3 py-2 text-right tabular-nums ${t.amount < 0 ? 'text-red-700' : ''}`}>
                    {formatChf(t.amount)}
                  </td>
                  <td className="px-3 py-2 text-right tabular-nums">{formatChf(t.balance)}</td>
                </tr>
              )
            })
          )}
        </tbody>
      </table>
    </div>
  )
}

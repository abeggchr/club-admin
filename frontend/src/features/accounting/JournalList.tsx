import { useMemo, useState } from 'react'
import { useQuery, useSuspenseQuery } from '@tanstack/react-query'
import { createColumnHelper, flexRender, tableFeatures, useTable } from '@tanstack/react-table'
import { MdAdd } from 'react-icons/md'
import { accountsQueryOptions, entriesQueryOptions } from './api'
import { BalanceAtDate } from './BalanceAtDate'
import { EntryForm } from './EntryForm'
import { formatChf } from './amount'
import { accountLabel } from './KontoPicker'
import type { JournalEntry } from './types'

const features = tableFeatures({})
const helper = createColumnHelper<typeof features, JournalEntry>()

/** What the form beside the table is doing: nothing, adding a new Buchung, or editing one. */
type FormMode = { kind: 'closed' } | { kind: 'add' } | { kind: 'edit'; id: number }

export function JournalList() {
  const [mode, setMode] = useState<FormMode>({ kind: 'closed' })
  const [dirty, setDirty] = useState(false)
  const { data: entries = [], isError } = useQuery(entriesQueryOptions)
  const { data: accounts } = useSuspenseQuery(accountsQueryOptions)
  const editing = mode.kind === 'edit' ? entries.find((e) => e.id === mode.id) : undefined

  function onSelect(id: number) {
    if (mode.kind === 'edit' && mode.id === id) return
    if (dirty && !window.confirm('Ungespeicherte Änderungen verwerfen?')) return
    setMode({ kind: 'edit', id })
  }

  const columns = useMemo(() => {
    const byNumber = new Map(accounts.map((a) => [a.number, a]))
    return helper.columns([
      helper.accessor('date', {
        header: 'Datum',
        cell: (info) => new Date(info.getValue()).toLocaleDateString('de-CH'),
      }),
      helper.accessor('text', { header: 'Text' }),
      helper.accessor('accountNumber', {
        header: 'Konto',
        cell: (info) => {
          const account = byNumber.get(info.getValue())
          return account ? accountLabel(account) : info.getValue()
        },
      }),
      helper.accessor('amount', {
        header: () => <span className="block text-right">Betrag</span>,
        cell: (info) => (
          <span className={`block text-right tabular-nums ${info.getValue() < 0 ? 'text-red-700' : ''}`}>
            {formatChf(info.getValue())}
          </span>
        ),
      }),
      helper.accessor('balance', {
        // Erwarteter Kontostand
        header: () => <span className="block text-right">Kontostand</span>,
        cell: (info) => <span className="block text-right tabular-nums">{formatChf(info.getValue())}</span>,
      }),
    ])
  }, [accounts])

  const table = useTable({ features, columns, data: entries })
  const rows = table.getRowModel().rows

  return (
    <>
      <div className="mt-4 flex items-center justify-between gap-4">
        <button
          type="button"
          onClick={() => setMode({ kind: 'add' })}
          disabled={dirty}
          aria-label="Neue Buchung"
          title={dirty ? 'Zuerst die ungespeicherten Änderungen speichern oder verwerfen' : 'Neue Buchung'}
          className="rounded bg-sky-600 p-2 text-white enabled:hover:bg-sky-700 disabled:opacity-40"
        >
          <MdAdd className="size-5" />
        </button>
        <BalanceAtDate />
      </div>

      {isError && <p className="mt-4 text-red-600">Buchungen konnten nicht geladen werden.</p>}

      <div className="mt-4 flex items-start gap-4">
        {(mode.kind === 'add' || editing) && (
          <aside className="w-1/3 shrink-0 rounded border border-slate-200 p-4">
            <EntryForm
              key={editing ? editing.id : 'new'}
              entry={editing}
              onDone={() => setMode({ kind: 'closed' })}
              onDirtyChange={setDirty}
            />
          </aside>
        )}
        <div className="min-w-0 flex-1 overflow-x-auto rounded border border-slate-200">
          <table className="w-full text-left text-sm">
            <thead className="border-b border-slate-200 bg-slate-50">
              {table.getHeaderGroups().map((headerGroup) => (
                <tr key={headerGroup.id}>
                  {headerGroup.headers.map((header) => (
                    <th key={header.id} className="px-3 py-2 font-medium">
                      {header.isPlaceholder ? null : flexRender(header.column.columnDef.header, header.getContext())}
                    </th>
                  ))}
                </tr>
              ))}
            </thead>
            <tbody>
              {rows.length === 0 ? (
                <tr>
                  <td colSpan={columns.length} className="px-3 py-8 text-center text-slate-500">
                    Noch keine Buchungen.
                  </td>
                </tr>
              ) : (
                rows.map((row) => {
                  const selected = mode.kind === 'edit' && mode.id === row.original.id
                  return (
                    <tr
                      key={row.id}
                      onClick={() => onSelect(row.original.id)}
                      aria-selected={selected}
                      className={`cursor-pointer border-b border-slate-100 last:border-0 ${
                        selected ? 'bg-sky-50 hover:bg-sky-100' : 'hover:bg-slate-50'
                      }`}
                    >
                      {row.getAllCells().map((cell) => (
                        <td key={cell.id} className="px-3 py-2">
                          {flexRender(cell.column.columnDef.cell, cell.getContext())}
                        </td>
                      ))}
                    </tr>
                  )
                })
              )}
            </tbody>
          </table>
        </div>
      </div>
    </>
  )
}

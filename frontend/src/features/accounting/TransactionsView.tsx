import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { MdAdd } from 'react-icons/md'
import { entriesQueryOptions, transactionsQueryOptions } from './api'
import { BalanceAtDate } from './BalanceAtDate'
import { EditPanel } from './EditPanel'
import { JournalList } from './JournalList'
import { TransactionsTable } from './TransactionsTable'

/** What the panel beside the Bewegungen is doing: nothing, adding a new Bewegung, or editing one. */
type PanelMode = { kind: 'closed' } | { kind: 'add' } | { kind: 'edit'; id: number }

/** The Buchhaltung page: Bewegungen | edit panel (only while open) | Buchungsjournal. */
export function TransactionsView() {
  const [mode, setMode] = useState<PanelMode>({ kind: 'closed' })
  const [dirty, setDirty] = useState(false)
  const { data: transactions = [], isError: transactionsFailed } = useQuery(transactionsQueryOptions)
  const { data: entries = [], isError: entriesFailed } = useQuery(entriesQueryOptions)
  const editing = mode.kind === 'edit' ? transactions.find((t) => t.id === mode.id) : undefined

  function onSelect(id: number) {
    if (mode.kind === 'edit' && mode.id === id) return
    if (dirty && !window.confirm('Ungespeicherte Änderungen verwerfen?')) return
    setMode({ kind: 'edit', id })
  }

  return (
    <>
      <div className="mt-4 flex items-center justify-between gap-4">
        <button
          type="button"
          onClick={() => setMode({ kind: 'add' })}
          disabled={dirty}
          aria-label="Neue Bewegung"
          title={dirty ? 'Zuerst die ungespeicherten Änderungen speichern oder verwerfen' : 'Neue Bewegung'}
          className="rounded bg-sky-600 p-2 text-white enabled:hover:bg-sky-700 disabled:opacity-40"
        >
          <MdAdd className="size-5" />
        </button>
        <BalanceAtDate />
      </div>

      {(transactionsFailed || entriesFailed) && (
        <p className="mt-4 text-red-600">Bewegungen konnten nicht geladen werden.</p>
      )}

      <div className="mt-4 flex items-start gap-4">
        <div className="flex min-w-0 flex-1">
          <TransactionsTable
            transactions={transactions}
            selectedId={mode.kind === 'edit' ? mode.id : null}
            onSelect={onSelect}
          />
        </div>
        {(mode.kind === 'add' || editing) && (
          <aside className="w-96 shrink-0 rounded border border-slate-200 p-4">
            <EditPanel
              key={editing ? editing.id : 'new'}
              transaction={editing}
              entries={editing ? entries.filter((e) => e.transactionId === editing.id) : []}
              onDone={() => setMode({ kind: 'closed' })}
              onDirtyChange={setDirty}
            />
          </aside>
        )}
        <div className="flex min-w-0 flex-[2]">
          <JournalList entries={entries} />
        </div>
      </div>
    </>
  )
}

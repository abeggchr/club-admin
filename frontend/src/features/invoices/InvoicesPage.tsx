import { useState } from 'react'
import { useSuspenseQuery } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { MdAdd } from 'react-icons/md'
import { formatChf } from '../accounting/amount'
import { textTemplatesQueryOptions } from '../settings/textTemplateApi'
import { formatDate, invoicesQueryOptions, statusLabel } from './api'
import { CreatePanel } from './CreatePanel'
import { DetailPanel } from './DetailPanel'

/** The Rechnungen area: table (newest first) | create panel (only while open). */
export function InvoicesPage() {
  const { data: invoices } = useSuspenseQuery(invoicesQueryOptions)
  const { data: templates } = useSuspenseQuery(textTemplatesQueryOptions)
  const [creating, setCreating] = useState(false)
  const [selectedId, setSelectedId] = useState<number | null>(null)
  const noTemplates = templates.length === 0
  const rows = [...invoices].sort((a, b) => b.id - a.id)

  return (
    <div className="px-8">
      <h2 className="mt-4 text-xl font-semibold text-sky-900">Rechnungen</h2>
      <div className="mt-4 flex items-center gap-4">
        <button
          type="button"
          onClick={() => {
            setSelectedId(null)
            setCreating(true)
          }}
          disabled={noTemplates || creating}
          aria-label="Neue Rechnung"
          title={noTemplates ? 'Zuerst unter Einstellungen eine Textvorlage erstellen' : 'Neue Rechnung'}
          className="rounded bg-sky-600 p-2 text-white enabled:hover:bg-sky-700 disabled:opacity-40"
        >
          <MdAdd className="size-5" />
        </button>
        {noTemplates && (
          <p className="text-sm text-slate-600">
            Zuerst unter{' '}
            <Link to="/settings" className="text-sky-700 underline hover:text-sky-900">
              Einstellungen
            </Link>{' '}
            eine Textvorlage erstellen.
          </p>
        )}
      </div>

      <div className="mt-4 flex items-start gap-4">
        <div className="min-w-0 flex-1 overflow-x-auto rounded border border-slate-200">
          <table className="w-full text-left text-sm">
            <thead className="border-b border-slate-200 bg-slate-50">
              <tr>
                <th className="px-3 py-2 font-medium">Nr.</th>
                <th className="px-3 py-2 font-medium">Datum</th>
                <th className="px-3 py-2 font-medium">Person</th>
                <th className="px-3 py-2 text-right font-medium">Total</th>
                <th className="px-3 py-2 font-medium">Status</th>
              </tr>
            </thead>
            <tbody>
              {rows.length === 0 ? (
                <tr>
                  <td colSpan={5} className="px-3 py-8 text-center text-slate-500">
                    Noch keine Rechnungen.
                  </td>
                </tr>
              ) : (
                rows.map((r) => (
                  <tr
                    key={r.id}
                    onClick={() => {
                      setCreating(false)
                      setSelectedId(r.id)
                    }}
                    className={`cursor-pointer border-b border-slate-100 last:border-0 hover:bg-sky-50 ${r.id === selectedId ? 'bg-sky-100' : ''}`}
                  >
                    <td className="px-3 py-2 tabular-nums">{r.number}</td>
                    <td className="px-3 py-2">{formatDate(r.date)}</td>
                    <td className="px-3 py-2">{r.person.name}</td>
                    <td className="px-3 py-2 text-right tabular-nums">{formatChf(r.total)}</td>
                    <td className="px-3 py-2">{statusLabel(r.status)}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
        {creating && (
          <aside className="flex-1 shrink-0 rounded border border-slate-200 p-4">
            <CreatePanel onDone={() => setCreating(false)} />
          </aside>
        )}
        {!creating && selectedId !== null && (
          <aside className="flex-1 shrink-0 rounded border border-slate-200 p-4">
            <DetailPanel key={selectedId} id={selectedId} onClose={() => setSelectedId(null)} />
          </aside>
        )}
      </div>
    </div>
  )
}

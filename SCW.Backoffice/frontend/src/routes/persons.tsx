import { useEffect, useMemo, useRef, useState } from 'react'
import { createFileRoute, type ErrorComponentProps } from "@tanstack/react-router";
import { queryOptions, useMutation, useQueryClient, useSuspenseQuery } from '@tanstack/react-query'
import { FiDownload, FiUpload, FiX } from 'react-icons/fi'
import {
  columnFilteringFeature,
  createColumnHelper,
  createFilteredRowModel,
  createPaginatedRowModel,
  createSortedRowModel,
  flexRender,
  globalFilteringFeature,
  rowPaginationFeature,
  rowSortingFeature,
  sortFn_alphanumeric,
  tableFeatures,
  useTable,
} from '@tanstack/react-table'
import type { Row, SortingState } from '@tanstack/react-table'
import { rankItem, rankings } from '@tanstack/match-sorter-utils'

type Person = {
  id: number
  firstName: string
  lastName: string
  dateOfBirth: string | null
  joinedOn: string | null
  leftOn: string | null
  email: string | null
  mobilePhone: string | null
  landlinePhone: string | null
  address: string | null
  postalCode: string | null
  city: string | null
  country: string | null
  notes: string | null
}

async function fetchPersons(): Promise<Person[]> {
  const res = await fetch('/api/persons')
  if (!res.ok) throw new Error(`Backend returned ${res.status}`)
  return res.json()
}

const personsQueryOptions = queryOptions({
  queryKey: ['persons'],
  queryFn: fetchPersons,
})

async function importPersons(file: File): Promise<number> {
  const body = new FormData()
  body.append('file', file)
  const res = await fetch('/api/persons/import', { method: 'POST', body })
  if (!res.ok) {
    const data = await res.json().catch(() => null)
    throw new Error(data?.error ?? `Backend returned ${res.status}`)
  }
  return (await res.json()).imported
}

async function updatePerson(person: Person): Promise<Person> {
  const res = await fetch(`/api/persons/${person.id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(person),
  })
  if (!res.ok) {
    const data = await res.json().catch(() => null)
    throw new Error(data?.error ?? `Backend returned ${res.status}`)
  }
  return res.json()
}

const textFields: { key: keyof Person; label: string; type?: string; multiline?: boolean }[] = [
  { key: 'firstName', label: 'Vorname' },
  { key: 'lastName', label: 'Nachname' },
  { key: 'dateOfBirth', label: 'Geburtstag', type: 'date' },
  { key: 'joinedOn', label: 'Eintritt', type: 'date' },
  { key: 'leftOn', label: 'Austritt', type: 'date' },
  { key: 'email', label: 'E-Mail', type: 'email' },
  { key: 'mobilePhone', label: 'Mobilfunknummer', type: 'tel' },
  { key: 'landlinePhone', label: 'Festnetznummer', type: 'tel' },
  { key: 'address', label: 'Adresse' },
  { key: 'postalCode', label: 'PLZ' },
  { key: 'city', label: 'Ort' },
  { key: 'country', label: 'Land' },
  { key: 'notes', label: 'Notizen', multiline: true },
]

function EditPanel({ person, onClose }: { person: Person; onClose: () => void }) {
  const queryClient = useQueryClient()
  const [draft, setDraft] = useState(person)
  const [error, setError] = useState<string | null>(null)
  const [saved, setSaved] = useState(false)
  const update = useMutation({
    mutationFn: updatePerson,
    onSuccess: (updated) => {
      setDraft(updated)
      setSaved(true)
      return queryClient.invalidateQueries({ queryKey: personsQueryOptions.queryKey })
    },
    onError: (err) => setError(err.message),
  })
  const busy = update.isPending

  function submit(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    setSaved(false)
    // Empty inputs are stored as null, matching the importer.
    const payload = Object.fromEntries(
      Object.entries(draft).map(([k, v]) => [k, v === '' ? null : v]),
    ) as Person
    update.mutate(payload)
  }

  return (
    <aside className="w-96 shrink-0 rounded border border-neutral-200 p-4">
      <form onSubmit={submit}>
        <div className="flex items-center justify-between">
          <h2 className="text-xl font-bold">Person bearbeiten</h2>
          <button type="button" onClick={onClose} aria-label="Schliessen" className="p-1">
            <FiX />
          </button>
        </div>
        {textFields.map((f) => {
          const common = {
            id: `person-${f.key}`,
            value: (draft[f.key] as string | null) ?? '',
            required: f.key === 'firstName' || f.key === 'lastName',
            onChange: (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
              setDraft({ ...draft, [f.key]: e.target.value })
              setSaved(false)
            },
            className: 'mt-1 block w-full rounded border border-neutral-300 px-2 py-1 text-sm',
          }
          return (
            <div key={f.key} className="mt-3">
              <label htmlFor={common.id} className="text-sm font-medium">
                {f.label}
              </label>
              {f.multiline ? (
                <textarea rows={4} {...common} />
              ) : (
                <input type={f.type ?? 'text'} {...common} />
              )}
            </div>
          )
        })}
        {error && <p className="mt-3 text-sm text-red-600">{error}</p>}
        {saved && <p className="mt-3 text-sm text-green-700">Gespeichert.</p>}
        <div className="mt-4 flex justify-end gap-2 text-sm">
          <button
            type="button"
            onClick={onClose}
            className="rounded border border-neutral-300 px-3 py-1"
          >
            Schliessen
          </button>
          <button
            type="submit"
            disabled={busy}
            className="rounded bg-blue-600 px-3 py-1 text-white disabled:opacity-40"
          >
            {busy ? 'Speichere…' : 'Speichern'}
          </button>
        </div>
      </form>
    </aside>
  )
}

function ImportDialog({ dialogRef }: { dialogRef: React.RefObject<HTMLDialogElement | null> }) {
  const queryClient = useQueryClient()
  const [file, setFile] = useState<File | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [imported, setImported] = useState<number | null>(null)
  const upload = useMutation({
    mutationFn: importPersons,
    onSuccess: (count) => {
      setImported(count)
      return queryClient.invalidateQueries({ queryKey: personsQueryOptions.queryKey })
    },
    onError: (err) => setError(err.message),
  })
  const busy = upload.isPending

  function reset() {
    setFile(null)
    setError(null)
    setImported(null)
  }

  function submit(e: React.FormEvent) {
    e.preventDefault()
    if (!file) return
    setError(null)
    upload.mutate(file)
  }

  return (
    <dialog
      ref={dialogRef}
      onClose={reset}
      className="m-auto w-[28rem] rounded border border-neutral-300 p-6 backdrop:bg-black/40"
    >
      <form onSubmit={submit}>
        <h2 className="text-xl font-bold">Personen importieren</h2>
        <input
          type="file"
          accept=".csv,text/csv"
          onChange={(e) => {
            setFile(e.target.files?.[0] ?? null)
            setError(null)
            setImported(null)
          }}
          className="mt-4 block w-full text-sm"
        />
        {error && <p className="mt-3 text-sm text-red-600">{error}</p>}
        {imported !== null && (
          <p className="mt-3 text-sm text-green-700">{imported} Personen importiert.</p>
        )}
        <div className="mt-6 flex justify-end gap-2 text-sm">
          <button
            type="button"
            onClick={() => dialogRef.current?.close()}
            className="rounded border border-neutral-300 px-3 py-1"
          >
            {imported !== null ? 'Schliessen' : 'Abbrechen'}
          </button>
          <button
            type="submit"
            disabled={!file || busy || imported !== null}
            className="rounded bg-blue-600 px-3 py-1 text-white disabled:opacity-40"
          >
            {busy ? 'Importiere…' : 'Importieren'}
          </button>
        </div>
      </form>
    </dialog>
  )
}

const features = tableFeatures({
  columnFilteringFeature,
  globalFilteringFeature,
  rowPaginationFeature,
  rowSortingFeature,
  sortFns: {
    alphanumeric: sortFn_alphanumeric,
  },
  filteredRowModel: createFilteredRowModel(),
  paginatedRowModel: createPaginatedRowModel(),
  sortedRowModel: createSortedRowModel(),
})

type Features = typeof features

const helper = createColumnHelper<Features, Person>()

function orDash(value: string | null | undefined) {
  return value ?? '—'
}

function formatDe(value: string | null): string {
  if (!value) return ''
  const [year, month, day] = value.split('-')
  return `${day}.${month}.${year}`
}

function formatDateDe(value: string | null): string {
  return value ? formatDe(value) : '—'
}

// Same headers and date format as the backend's PersonCsvImporter, so an
// exported file can be re-imported unchanged.
const exportColumns: { header: string; value: (p: Person) => string | null }[] = [
  { header: 'Vorname', value: (p) => p.firstName },
  { header: 'Nachname', value: (p) => p.lastName },
  { header: 'Geburtstag', value: (p) => formatDe(p.dateOfBirth) },
  { header: 'Eintritt', value: (p) => formatDe(p.joinedOn) },
  { header: 'Austritt', value: (p) => formatDe(p.leftOn) },
  { header: 'E-Mail', value: (p) => p.email },
  { header: 'Mobilfunknummer', value: (p) => p.mobilePhone },
  { header: 'Festnetznummer', value: (p) => p.landlinePhone },
  { header: 'Adresse', value: (p) => p.address },
  { header: 'PLZ', value: (p) => p.postalCode },
  { header: 'Ort', value: (p) => p.city },
  { header: 'Land', value: (p) => p.country },
  { header: 'Notizen', value: (p) => p.notes },
]

function buildPersonCsv(persons: Person[]): string {
  const esc = (v: string | null) => {
    const s = v ?? ''
    return /[";\n\r]/.test(s) ? `"${s.replaceAll('"', '""')}"` : s
  }
  return [
    exportColumns.map((c) => c.header).join(';'),
    ...persons.map((p) => exportColumns.map((c) => esc(c.value(p))).join(';')),
  ].join('\r\n')
}

const columns = helper.columns([
  helper.accessor('firstName', { header: 'Vorname' }),
  helper.accessor('lastName', { header: 'Nachname' }),
  helper.accessor('dateOfBirth', {
    header: 'Geburtstag',
    cell: (info) => formatDateDe(info.getValue()),
  }),
  helper.accessor('joinedOn', {
    header: 'Eintritt',
    cell: (info) => formatDateDe(info.getValue()),
  }),
  helper.accessor('leftOn', {
    header: 'Austritt',
    cell: (info) => formatDateDe(info.getValue()),
  }),
  helper.accessor('email', {
    header: 'E-Mail',
    cell: (info) => {
      const email = info.getValue()
      return email ? (
        <a className="text-blue-600 hover:underline" href={`mailto:${email}`}>
          {email}
        </a>
      ) : orDash(email)
    },
  }),
  helper.accessor('mobilePhone', {
    header: 'Mobilfunknummer',
    cell: (info) => orDash(info.getValue()),
  }),
  helper.accessor('landlinePhone', {
    header: 'Festnetznummer',
    cell: (info) => orDash(info.getValue()),
  }),
  helper.accessor('address', {
    header: 'Adresse',
    cell: (info) => orDash(info.getValue()),
  }),
  helper.accessor('postalCode', {
    header: 'PLZ',
    cell: (info) => orDash(info.getValue()),
  }),
  helper.accessor('city', {
    header: 'Ort',
    cell: (info) => orDash(info.getValue()),
  }),
  helper.accessor('country', {
    header: 'Land',
    cell: (info) => orDash(info.getValue()),
  }),
  helper.accessor('notes', {
    header: 'Notizen',
    cell: (info) => orDash(info.getValue()),
  }),
])

function hideColumns(...keys: string[]) {
  return columns.filter((c) => !('accessorKey' in c && keys.includes(c.accessorKey)));
}

const views = {
  active: {
    label: 'Aktive',
    filter: (p: Person) => !p.leftOn,
    columns: hideColumns('leftOn', 'dateOfBirth', 'joinedOn', 'landlinePhone', 'country', 'notes'),
  },
  contacts: {
    label: 'Kontakte',
    filter: (p: Person) => !p.joinedOn,
    columns: hideColumns('dateOfBirth', 'joinedOn', 'leftOn', 'country', 'notes'),
  },
  former: {
    label: 'Ehemalige',
    filter: (p: Person) => !!p.leftOn,
    columns: hideColumns('dateOfBirth', 'country', 'notes'),
  },
  all: {
    label: 'Alle',
    filter: (p: Person) => !!p.leftOn,
    columns,
  },
}

type ViewKey = keyof typeof views

function globalSearchFilter(row: Row<Features, Person>, _columnId: string, filterValue: unknown) {
  const query = String(filterValue ?? '').trim()
  if (!query) return true
  return (
    rankItem(row.original, query, {
      accessors: [
        (p) => p.firstName,
        (p) => p.lastName,
        (p) => p.email ?? '',
        (p) => p.city ?? '',
        (p) => p.country ?? '',
        (p) => p.notes ?? '',
      ],
    }).rank > rankings.NO_MATCH
  )
}

export const Route = createFileRoute('/persons')({
  loader: ({ context: { queryClient } }) => queryClient.ensureQueryData(personsQueryOptions),
  component: Persons,
  pendingComponent: () => <p className="p-8">Loading…</p>,
  errorComponent: (error: ErrorComponentProps) => (
    <p className="p-8 text-red-600">Failed to load: {JSON.stringify(error)}</p>
  ),
})

function Persons() {
  const { data: persons } = useSuspenseQuery(personsQueryOptions)
  const [sorting, setSorting] = useState<SortingState>([
    { id: 'lastName', desc: false },
    { id: 'firstName', desc: false },
  ])
  const [filter, setFilter] = useState('')
  const [debouncedFilter, setDebouncedFilter] = useState('')
  const [viewKey, setViewKey] = useState<ViewKey>('active')
  const [selectedId, setSelectedId] = useState<number | null>(null)
  const selected = persons.find((p) => p.id === selectedId) ?? null
  const importDialog = useRef<HTMLDialogElement>(null)
  const view = views[viewKey]
  const viewPersons = useMemo(() => persons.filter(view.filter), [persons, view])

  useEffect(() => {
    const timer = setTimeout(() => setDebouncedFilter(filter), 300)
    return () => clearTimeout(timer)
  }, [filter])

  const table = useTable({
    features,
    columns: view.columns,
    data: viewPersons,
    state: { sorting, globalFilter: debouncedFilter },
    onSortingChange: setSorting,
    onGlobalFilterChange: setDebouncedFilter,
    globalFilterFn: globalSearchFilter,
    defaultColumn: { sortFn: 'alphanumeric' },
    initialState: { pagination: { pageIndex: 0, pageSize: 25 } },
  })

  const rows = table.getRowModel().rows
  const totalRows = table.getRowCount()

  function exportCsv() {
    const filtered = table.getFilteredRowModel().rows.map((r) => r.original)
    // BOM so Excel opens the UTF-8 file with the correct encoding.
    const url = URL.createObjectURL(new Blob([`\uFEFF${buildPersonCsv(filtered)}`], {
      type: 'text/csv;charset=utf-8',
    }))
    const a = Object.assign(document.createElement('a'), { href: url, download: 'personen.csv' })
    a.click()
    URL.revokeObjectURL(url)
  }

  return (
    <div className="px-8">
      <div className="mt-4 flex items-center gap-4">
        <input
          type="search"
          placeholder="Suchen (Name, E-Mail, Ort, Land, Notizen)…"
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
          className="w-96 rounded border border-neutral-300 px-3 py-2 text-sm"
        />
        <div role="group" aria-label="Ansicht" className="inline-flex">
          {Object.entries(views).map(([key, v]) => (
            <button
              key={key}
              type="button"
              aria-pressed={key === viewKey}
              onClick={() => setViewKey(key as ViewKey)}
              className={`-ml-px border border-neutral-300 px-3 py-2 text-sm first:ml-0 first:rounded-l last:rounded-r ${
                key === viewKey ? 'bg-neutral-800 text-white' : 'hover:bg-neutral-50'
              }`}
            >
              {v.label}
            </button>
          ))}
        </div>
        <div className="ml-auto flex gap-2">
          <button
            type="button"
            onClick={exportCsv}
            className="flex items-center gap-2 rounded border border-neutral-300 px-3 py-2 text-sm hover:bg-neutral-50"
          >
            <FiDownload />
            Export
          </button>
          <button
            type="button"
            onClick={() => importDialog.current?.showModal()}
            className="flex items-center gap-2 rounded border border-neutral-300 px-3 py-2 text-sm hover:bg-neutral-50"
          >
            <FiUpload />
            Import
          </button>
        </div>
      </div>
      <ImportDialog dialogRef={importDialog} />

      <div className="mt-4 flex items-start gap-4">
      <div className="min-w-0 flex-1">
      <div className="overflow-x-auto rounded border border-neutral-200">
        <table className="w-full text-left text-sm">
          <thead className="border-b border-neutral-200 bg-neutral-50">
            {table.getHeaderGroups().map((headerGroup) => (
              <tr key={headerGroup.id}>
                {headerGroup.headers.map((header) => (
                  <th key={header.id} className="px-3 py-2 font-medium">
                    {header.isPlaceholder ? null : (
                      <button
                        type="button"
                        onClick={header.column.getToggleSortingHandler()}
                        disabled={!header.column.getCanSort()}
                        className="flex items-center gap-1"
                      >
                        {flexRender(header.column.columnDef.header, header.getContext())}
                        {header.column.getIsSorted() === 'asc'
                          ? ' ↑'
                          : header.column.getIsSorted() === 'desc'
                            ? ' ↓'
                            : ''}
                      </button>
                    )}
                  </th>
                ))}
              </tr>
            ))}
          </thead>
          <tbody>
            {rows.length === 0 ? (
              <tr>
                <td colSpan={view.columns.length} className="px-3 py-8 text-center text-neutral-500">
                  {persons.length === 0 ? 'Keine Personen vorhanden.' : 'Keine Personen gefunden.'}
                </td>
              </tr>
            ) : (
              rows.map((row) => (
                <tr
                  key={row.id}
                  onClick={() => setSelectedId(row.original.id)}
                  aria-selected={row.original.id === selectedId}
                  className={`cursor-pointer border-b border-neutral-100 last:border-0 ${
                    row.original.id === selectedId
                      ? 'bg-blue-50 hover:bg-blue-100'
                      : 'hover:bg-neutral-50'
                  }`}
                >
                  {row.getAllCells().map((cell) => (
                    <td key={cell.id} className="whitespace-nowrap px-3 py-2">
                      {flexRender(cell.column.columnDef.cell, cell.getContext())}
                    </td>
                  ))}
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      <div className="mt-4 flex items-center justify-end gap-4 text-sm text-neutral-600">
        <span>
          {rows.length} von {totalRows}
        </span>
        <button
          type="button"
          onClick={() => table.previousPage()}
          disabled={!table.getCanPreviousPage()}
          className="rounded border border-neutral-300 px-3 py-1 disabled:opacity-40"
        >
          Zurück
        </button>
        <span>
          Seite {table.state.pagination.pageIndex + 1} von {table.getPageCount()}
        </span>
        <button
          type="button"
          onClick={() => table.nextPage()}
          disabled={!table.getCanNextPage()}
          className="rounded border border-neutral-300 px-3 py-1 disabled:opacity-40"
        >
          Weiter
        </button>
      </div>
      </div>
      {selected && (
        <EditPanel key={selected.id} person={selected} onClose={() => setSelectedId(null)} />
      )}
      </div>
    </div>
  )
}

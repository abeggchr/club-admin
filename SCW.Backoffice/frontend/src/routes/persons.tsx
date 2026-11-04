import { useEffect, useState } from 'react'
import { createFileRoute, type ErrorComponentProps } from "@tanstack/react-router";
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

const columns = helper.columns([
  helper.accessor('firstName', { header: 'Vorname' }),
  helper.accessor('lastName', { header: 'Nachname' }),
  helper.accessor('dateOfBirth', {
    header: 'Geburtsdatum',
    cell: (info) => orDash(info.getValue()),
  }),
  helper.accessor('joinedOn', {
    header: 'Beitritt',
    cell: (info) => orDash(info.getValue()),
  }),
  helper.accessor('leftOn', {
    header: 'Austritt',
    cell: (info) => orDash(info.getValue()),
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
    header: 'Mobiltelefon',
    cell: (info) => orDash(info.getValue()),
  }),
  helper.accessor('landlinePhone', {
    header: 'Festnetz',
    cell: (info) => orDash(info.getValue()),
  }),
  helper.accessor('address', {
    header: 'Adresse',
    cell: (info) => orDash(info.getValue()),
  }),
  helper.accessor('postalCode', {
    header: 'Postleitzahl',
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
  loader: fetchPersons,
  component: Persons,
  pendingComponent: () => <p className="p-8">Loading…</p>,
  errorComponent: (error: ErrorComponentProps) => (
    <p className="p-8 text-red-600">Failed to load: {JSON.stringify(error)}</p>
  ),
})

function Persons() {
  const persons = Route.useLoaderData()
  const [sorting, setSorting] = useState<SortingState>([
    { id: 'lastName', desc: false },
    { id: 'firstName', desc: false },
  ])
  const [filter, setFilter] = useState('')
  const [debouncedFilter, setDebouncedFilter] = useState('')

  useEffect(() => {
    const timer = setTimeout(() => setDebouncedFilter(filter), 300)
    return () => clearTimeout(timer)
  }, [filter])

  const table = useTable({
    features,
    columns,
    data: persons,
    state: { sorting, globalFilter: debouncedFilter },
    onSortingChange: setSorting,
    onGlobalFilterChange: setDebouncedFilter,
    globalFilterFn: globalSearchFilter,
    defaultColumn: { sortFn: 'alphanumeric' },
    initialState: { pagination: { pageIndex: 0, pageSize: 25 } },
  })

  const rows = table.getRowModel().rows
  const totalRows = table.getRowCount()

  return (
    <div className="p-8">
      <h1 className="text-3xl font-bold">Personen</h1>

      <input
        type="search"
        placeholder="Suchen (Name, E-Mail, Ort, Land, Notizen)…"
        value={filter}
        onChange={(e) => setFilter(e.target.value)}
        className="mt-4 w-96 rounded border border-neutral-300 px-3 py-2 text-sm"
      />

      <div className="mt-4 overflow-x-auto rounded border border-neutral-200">
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
                <td colSpan={columns.length} className="px-3 py-8 text-center text-neutral-500">
                  {persons.length === 0 ? 'Keine Personen vorhanden.' : 'Keine Personen gefunden.'}
                </td>
              </tr>
            ) : (
              rows.map((row) => (
                <tr
                  key={row.id}
                  className="border-b border-neutral-100 last:border-0 hover:bg-neutral-50"
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
  )
}

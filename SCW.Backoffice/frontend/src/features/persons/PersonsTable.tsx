import { flexRender } from '@tanstack/react-table'
import type { ReactTable } from '@tanstack/react-table'
import type { Features } from './columns'
import type { Person } from './types'

type Props = {
  table: ReactTable<Features, Person>
  emptyMessage: string
  selectedId: number | null
  onSelect: (id: number) => void
}

export function PersonsTable({ table, emptyMessage, selectedId, onSelect }: Props) {
  const rows = table.getRowModel().rows
  const totalRows = table.getRowCount()

  return (
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
                <td
                  colSpan={table.options.columns.length}
                  className="px-3 py-8 text-center text-neutral-500"
                >
                  {emptyMessage}
                </td>
              </tr>
            ) : (
              rows.map((row) => (
                <tr
                  key={row.id}
                  onClick={() => onSelect(row.original.id)}
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
  )
}

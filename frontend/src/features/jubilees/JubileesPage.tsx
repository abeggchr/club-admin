import { useSuspenseQuery } from '@tanstack/react-query'
import { formatDe } from '../persons/format'
import { jubileesQueryOptions, sortJubilees, type JubileePerson } from './api'

const emptyMessage = 'Keine Jubiläen im laufenden Clubjahr'

function Table({
  title,
  rows,
  dateLabel,
}: {
  title: string
  rows: JubileePerson[]
  dateLabel: string
}) {
  return (
    <section className="mt-8">
      <h3 className="text-lg font-semibold text-sky-900">{title}</h3>
      <div className="mt-2 overflow-x-auto rounded border border-slate-200">
        <table className="w-full text-left text-sm">
          <thead className="border-b border-slate-200 bg-slate-50">
            <tr>
              <th className="px-3 py-2 font-medium">Name</th>
              <th className="px-3 py-2 font-medium">Jahre</th>
              <th className="px-3 py-2 font-medium">{dateLabel}</th>
            </tr>
          </thead>
          <tbody>
            {rows.length === 0 ? (
              <tr>
                <td colSpan={3} className="px-3 py-8 text-center text-slate-500">
                  {emptyMessage}
                </td>
              </tr>
            ) : (
              rows.map((r) => (
                <tr key={r.personId} className="border-b border-slate-100 last:border-0">
                  <td className="px-3 py-2">
                    {r.lastName}, {r.firstName}
                  </td>
                  <td className="px-3 py-2 tabular-nums">{r.years}</td>
                  <td className="px-3 py-2">{formatDe(r.birthdate ?? r.entryDate ?? null)}</td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </section>
  )
}

/** The read-only Jubiliäen view: 10-year milestones of the current club year. */
export function JubileesPage() {
  const { data: jubilees } = useSuspenseQuery(jubileesQueryOptions)

  return (
    <div className="px-8">
      <h2 className="mt-4 text-xl font-semibold text-sky-900">
        Jubiläen {jubilees.clubYearStartYear}
      </h2>
      <Table
        title="Geburtstag"
        rows={sortJubilees(jubilees.anniversaries)}
        dateLabel="Geburtsdatum"
      />
      <Table title="Jubiläum" rows={sortJubilees(jubilees.jubilees)} dateLabel="Eintrittsdatum" />
    </div>
  )
}

import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { expectedBalanceQueryOptions } from './api'
import { formatChf } from './amount'

function today(): string {
  const d = new Date()
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

export function BalanceAtDate() {
  const [date, setDate] = useState(today)
  const { data, isError } = useQuery({ ...expectedBalanceQueryOptions(date), enabled: date !== '' })

  return (
    <div className="mt-6 flex items-end gap-4">
      <label className="flex flex-col text-sm">
        Kontostand per Datum
        <input
          type="date"
          value={date}
          onChange={(e) => setDate(e.target.value)}
          className="rounded border border-slate-300 p-2"
        />
      </label>
      <p className="pb-2 text-lg tabular-nums">
        {isError ? (
          <span className="text-red-600">Kontostand konnte nicht geladen werden.</span>
        ) : data ? (data.balance === null ? 'vor dem Anfangssaldo' : `CHF ${formatChf(data.balance)}`) : ''}
      </p>
    </div>
  )
}

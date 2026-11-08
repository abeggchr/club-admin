import { useQuery } from '@tanstack/react-query'
import { expectedBalanceQueryOptions } from './api'
import { formatChf } from './amount'

function today(): string {
  const d = new Date()
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

/** The Erwartete Kontostand as of today, on one line. */
export function BalanceAtDate() {
  const { data, isError } = useQuery(expectedBalanceQueryOptions(today()))

  return (
    <p className="flex items-baseline gap-2 text-sm">
      <span className="text-slate-600">Kontostand heute</span>
      <span
        className={`text-lg tabular-nums ${data?.differs ? 'text-red-600' : ''}`}
        title={data?.differs ? 'Bewegungen und Buchungsjournal stimmen nicht überein.' : undefined}
      >
        {isError ? (
          <span className="text-red-600">konnte nicht geladen werden.</span>
        ) : data ? (data.balance === null ? 'vor dem Anfangssaldo' : `CHF ${formatChf(data.balance)}`) : ''}
      </span>
    </p>
  )
}

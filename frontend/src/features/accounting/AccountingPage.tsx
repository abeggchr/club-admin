import { useSuspenseQuery } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { openingBalanceQueryOptions } from './api'
import { JournalList } from './JournalList'

export function AccountingPage() {
  const { data: openingBalance } = useSuspenseQuery(openingBalanceQueryOptions)

  return (
    <div className="px-8">
      <h2 className="mt-4 text-xl font-semibold text-sky-900">Buchhaltung</h2>
      {openingBalance ? (
        <JournalList />
      ) : (
        <p className="mt-4">
          <Link to="/settings" className="text-sky-700 underline hover:text-sky-900">
            Anfangssaldo in den Einstellungen erfassen
          </Link>
        </p>
      )}
    </div>
  )
}

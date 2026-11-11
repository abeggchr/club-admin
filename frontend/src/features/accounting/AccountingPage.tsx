import { useSuspenseQuery } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { settingsQueryOptions } from './api'
import { TransactionsView } from './TransactionsView'

export function AccountingPage() {
  const { data: settings } = useSuspenseQuery(settingsQueryOptions)

  return (
    <div className="px-8">
      <h2 className="mt-4 text-xl font-semibold text-sky-900">Buchhaltung</h2>
      {settings ? (
        <TransactionsView />
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

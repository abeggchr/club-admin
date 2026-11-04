import { createFileRoute, type ErrorComponentProps } from "@tanstack/react-router";
import { openingBalanceQueryOptions } from '../features/accounting/api'
import { AccountingPage } from '../features/accounting/AccountingPage'

export const Route = createFileRoute('/accounting')({
  loader: ({ context: { queryClient } }) => queryClient.ensureQueryData(openingBalanceQueryOptions),
  component: AccountingPage,
  pendingComponent: () => <p className="p-8">Laden…</p>,
  errorComponent: ({ error }: ErrorComponentProps) => (
    <p className="p-8 text-red-600">Laden fehlgeschlagen: {error instanceof Error ? error.message : String(error)}</p>
  ),
})

import { createFileRoute, type ErrorComponentProps } from "@tanstack/react-router";
import { personsQueryOptions } from '../features/persons/api'
import { PersonsPage } from '../features/persons/PersonsPage'

export const Route = createFileRoute('/persons')({
  loader: ({ context: { queryClient } }) => queryClient.ensureQueryData(personsQueryOptions),
  component: PersonsPage,
  pendingComponent: () => <p className="p-8">Loading…</p>,
  errorComponent: (error: ErrorComponentProps) => (
    <p className="p-8 text-red-600">Failed to load: {JSON.stringify(error)}</p>
  ),
})

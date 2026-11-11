import { createFileRoute, type ErrorComponentProps } from '@tanstack/react-router'
import { jubileesQueryOptions } from '../features/jubilees/api'
import { JubileesPage } from '../features/jubilees/JubileesPage'

export const Route = createFileRoute('/jubilees')({
  loader: ({ context: { queryClient } }) => queryClient.ensureQueryData(jubileesQueryOptions),
  component: JubileesPage,
  pendingComponent: () => <p className="p-8">Loading…</p>,
  errorComponent: (error: ErrorComponentProps) => (
    <p className="p-8 text-red-600">Failed to load: {JSON.stringify(error)}</p>
  ),
})

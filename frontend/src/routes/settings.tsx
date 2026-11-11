import { createFileRoute, type ErrorComponentProps } from '@tanstack/react-router'
import { settingsQueryOptions } from '../features/accounting/api'
import { SettingsPage } from '../features/settings/SettingsPage'

export const Route = createFileRoute('/settings')({
  loader: ({ context: { queryClient } }) => queryClient.ensureQueryData(settingsQueryOptions),
  component: SettingsPage,
  pendingComponent: () => <p className="p-8">Laden…</p>,
  errorComponent: ({ error }: ErrorComponentProps) => (
    <p className="p-8 text-red-600">Laden fehlgeschlagen: {error instanceof Error ? error.message : String(error)}</p>
  ),
})

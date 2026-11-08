import { queryOptions, type QueryClient } from '@tanstack/react-query'
import { getJson, requestJson } from '../accounting/api'
import type { InvoiceInput } from './invoiceDraft'

export type InvoiceListItem = {
  id: number
  number: string
  /** ISO yyyy-mm-dd */
  date: string
  person: { id: number; name: string }
  total: number
  status: string
}

export const invoicesQueryOptions = queryOptions({
  queryKey: ['invoices'],
  queryFn: () => getJson<InvoiceListItem[]>('/api/invoices'),
})

/** Creates the Rechnung (the backend renders and stores the PDF); rejects with the backend's message. */
export function createInvoice(input: InvoiceInput): Promise<unknown> {
  return requestJson('/api/invoices', 'POST', input)
}

export function invalidateInvoices(queryClient: QueryClient) {
  return queryClient.invalidateQueries({ queryKey: invoicesQueryOptions.queryKey })
}

const statusLabels: Record<string, string> = { Created: 'Erstellt' }

export function statusLabel(status: string): string {
  return statusLabels[status] ?? status
}

/** yyyy-mm-dd to dd.MM.yyyy */
export function formatDate(iso: string): string {
  const [y, m, d] = iso.split('-')
  return `${d}.${m}.${y}`
}

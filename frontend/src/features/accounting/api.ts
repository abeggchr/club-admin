import { queryOptions, type QueryClient } from '@tanstack/react-query'
import type { Account, JournalEntry, Transaction, TransactionInput } from './types'

export type OpeningBalance = {
  openingBalance: number
  startDate: string
}

/** Throws the backend's German error message, or a generic one, for a non-OK response. */
export async function ensureOk(res: Response): Promise<void> {
  if (res.ok) return
  const data = await res.json().catch(() => null)
  throw new Error(data?.error ?? `Backend returned ${res.status}`)
}

export async function requestJson<T>(url: string, method: 'POST' | 'PUT', body: unknown): Promise<T> {
  const res = await fetch(url, {
    method,
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  await ensureOk(res)
  return res.json()
}

export async function getJson<T>(url: string): Promise<T> {
  const res = await fetch(url)
  await ensureOk(res)
  return res.json()
}

export const accountsQueryOptions = queryOptions({
  queryKey: ['accounts'],
  queryFn: () => getJson<Account[]>('/api/accounts'),
  staleTime: Infinity, // the Kontoplan only changes with a deploy
})

async function fetchOpeningBalance(): Promise<OpeningBalance | null> {
  const res = await fetch('/api/opening-balance')
  if (res.status === 404) return null
  await ensureOk(res)
  return res.json()
}

export const openingBalanceQueryOptions = queryOptions({
  queryKey: ['opening-balance'],
  queryFn: fetchOpeningBalance,
})

export function saveOpeningBalance(balance: OpeningBalance): Promise<OpeningBalance> {
  return requestJson('/api/opening-balance', 'PUT', balance)
}

export type JournalFilters = {
  from: string
  to: string
  accountNumber: string
  search: string
}

export const noFilters: JournalFilters = { from: '', to: '', accountNumber: '', search: '' }

/** Filters only hide rows; the backend keeps the running balance of the whole journal. */
function fetchEntries(filters: JournalFilters): Promise<JournalEntry[]> {
  const params = new URLSearchParams()
  if (filters.from) params.set('from', filters.from)
  if (filters.to) params.set('to', filters.to)
  if (filters.accountNumber) params.set('account', filters.accountNumber)
  if (filters.search.trim()) params.set('q', filters.search.trim())
  return getJson(`/api/entries?${params}`)
}

export const entriesQueryOptions = queryOptions({
  queryKey: ['entries'],
  queryFn: () => fetchEntries(noFilters),
})

export const transactionsQueryOptions = queryOptions({
  queryKey: ['transactions'],
  queryFn: () => getJson<Transaction[]>('/api/transactions'),
})

/** Creates a Bewegung (id null) or replaces an existing one, with all its Buchungen in one request. */
export function saveTransaction(id: number | null, input: TransactionInput): Promise<Transaction> {
  return requestJson(id === null ? '/api/transactions' : `/api/transactions/${id}`, id === null ? 'POST' : 'PUT', input)
}

/** Deletes a Bewegung together with any Buchungen still stored for it. */
export async function deleteTransaction(id: number): Promise<void> {
  await ensureOk(await fetch(`/api/transactions/${id}`, { method: 'DELETE' }))
}

/** Refreshes everything derived from the journal after a Bewegung or the Anfangssaldo changed. */
export async function invalidateJournalQueries(queryClient: QueryClient): Promise<void> {
  await queryClient.invalidateQueries({ queryKey: entriesQueryOptions.queryKey })
  await queryClient.invalidateQueries({ queryKey: transactionsQueryOptions.queryKey })
  await queryClient.invalidateQueries({ queryKey: ['expected-balance'] })
}

export type ExpectedBalance = {
  date: string
  /** Erwarteter Kontostand from the Buchungen as of the date. */
  balance: number | null
  /** Kontostand from the Bewegungen (the bank's view) as of the date. */
  transactionBalance: number | null
  /** True when the two views disagree; "Kontostand heute" turns red. */
  differs: boolean
}

export const expectedBalanceQueryOptions = (date: string) =>
  queryOptions({
    queryKey: ['expected-balance', date],
    queryFn: () => getJson<ExpectedBalance>(`/api/expected-balance?date=${encodeURIComponent(date)}`),
  })

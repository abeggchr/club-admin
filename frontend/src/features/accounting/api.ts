import { queryOptions } from '@tanstack/react-query'
import type { Account, Entry, EntryInput, JournalEntry } from './types'

export type OpeningBalance = {
  amount: number
  startDate: string
}

async function fetchAccounts(): Promise<Account[]> {
  const res = await fetch('/api/accounts')
  if (!res.ok) throw new Error(`Backend returned ${res.status}`)
  return res.json()
}

export const accountsQueryOptions = queryOptions({
  queryKey: ['accounts'],
  queryFn: fetchAccounts,
  staleTime: Infinity, // the Kontoplan only changes with a deploy
})

async function fetchOpeningBalance(): Promise<OpeningBalance | null> {
  const res = await fetch('/api/opening-balance')
  if (res.status === 404) return null
  if (!res.ok) throw new Error(`Backend returned ${res.status}`)
  return res.json()
}

export const openingBalanceQueryOptions = queryOptions({
  queryKey: ['opening-balance'],
  queryFn: fetchOpeningBalance,
})

export async function saveOpeningBalance(balance: OpeningBalance): Promise<OpeningBalance> {
  const res = await fetch('/api/opening-balance', {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(balance),
  })
  if (!res.ok) {
    const data = await res.json().catch(() => null)
    throw new Error(data?.error ?? `Backend returned ${res.status}`)
  }
  return res.json()
}

export const entriesQueryOptions = queryOptions({
  queryKey: ['entries'],
  queryFn: async (): Promise<JournalEntry[]> => {
    const res = await fetch('/api/entries')
    if (!res.ok) throw new Error(`Backend returned ${res.status}`)
    return res.json()
  },
})

export async function createEntry(input: EntryInput): Promise<Entry> {
  const res = await fetch('/api/entries', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(input),
  })
  if (!res.ok) {
    const data = await res.json().catch(() => null)
    throw new Error(data?.error ?? `Backend returned ${res.status}`)
  }
  return res.json()
}

export type ExpectedBalance = { date: string; balance: number | null }

export const expectedBalanceQueryOptions = (date: string) =>
  queryOptions({
    queryKey: ['expected-balance', date],
    queryFn: async (): Promise<ExpectedBalance> => {
      const res = await fetch(`/api/expected-balance?date=${encodeURIComponent(date)}`)
      if (!res.ok) throw new Error(`Backend returned ${res.status}`)
      return res.json()
    },
  })

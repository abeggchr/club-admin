import { queryOptions } from '@tanstack/react-query'
import type { Account } from './types'

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

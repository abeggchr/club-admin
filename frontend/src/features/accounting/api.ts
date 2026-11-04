import { queryOptions } from '@tanstack/react-query'

export type OpeningBalance = {
  amount: number
  startDate: string
}

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

import { useSuspenseQuery } from '@tanstack/react-query'
import { accountsQueryOptions } from './api'
import type { Account } from './types'

export function accountLabel(account: Account): string {
  return `${account.number} ${account.name}`
}

type Props = {
  value: number | null
  onChange: (number: number | null) => void
  id?: string
  className?: string
}

/**
 * Konto picker: "3400 Jahresbeiträge", grouped by Einnahmen and Ausgaben.
 * Inactive Konten are not selectable; an inactive Konto that is the current
 * value stays shown (disabled) so existing Buchungen remain resolvable.
 */
export function KontoPicker({ value, onChange, id, className }: Props) {
  const { data: accounts } = useSuspenseQuery(accountsQueryOptions)

  const visible = (category: Account['category']) =>
    accounts.filter((a) => a.category === category && (a.active || a.number === value))

  const groups = [
    { label: 'Einnahmen', accounts: visible('Income') },
    { label: 'Ausgaben', accounts: visible('Expense') },
  ]

  return (
    <select
      id={id}
      className={className}
      value={value ?? ''}
      onChange={(e) => onChange(e.target.value === '' ? null : Number(e.target.value))}
    >
      <option value="">Konto wählen…</option>
      {groups.map((group) => (
        <optgroup key={group.label} label={group.label}>
          {group.accounts.map((a) => (
            <option key={a.number} value={a.number} disabled={!a.active}>
              {accountLabel(a)}
              {a.active ? '' : ' (inaktiv)'}
            </option>
          ))}
        </optgroup>
      ))}
    </select>
  )
}

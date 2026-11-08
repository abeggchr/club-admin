import { useSuspenseQuery } from '@tanstack/react-query'
import { useId, useState } from 'react'
import { accountsQueryOptions } from './api'
import { filterAccounts } from './kontoFilter'
import type { Account } from './types'

export function accountLabel(account: Account): string {
  return `${account.number} ${account.name}`
}

const categoryLabel: Record<Account['category'], string> = {
  Income: 'Einnahme',
  Expense: 'Ausgabe',
}

const categoryStyle: Record<Account['category'], string> = {
  Income: 'bg-green-100 text-green-800',
  Expense: 'bg-red-100 text-red-800',
}

type Props = {
  value: number | null
  onChange: (number: number | null) => void
  id?: string
  className?: string
}

/**
 * Konto autocomplete: type a Kontonummer or part of the name to filter; every
 * option shows whether the Konto is an Einnahme or an Ausgabe. Inactive Konten
 * are not selectable; an inactive Konto that is the current value stays shown
 * (disabled) so existing Buchungen remain resolvable.
 */
export function KontoPicker({ value, onChange, id, className }: Props) {
  const { data: accounts } = useSuspenseQuery(accountsQueryOptions)
  const listId = useId()
  const [query, setQuery] = useState<string | null>(null) // null = not editing
  const [open, setOpen] = useState(false)
  const [highlight, setHighlight] = useState(0)

  const selected = accounts.find((a) => a.number === value) ?? null
  const text = query ?? (selected ? accountLabel(selected) : '')
  // Show the full list until the user actually types something.
  const options = filterAccounts(accounts, query ?? '', value)

  const close = () => {
    setOpen(false)
    setQuery(null)
  }

  const choose = (a: Account) => {
    if (!a.active) return
    onChange(a.number)
    close()
  }

  const onKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'ArrowDown') {
      e.preventDefault()
      setOpen(true)
      setHighlight((h) => Math.min(h + 1, options.length - 1))
    } else if (e.key === 'ArrowUp') {
      e.preventDefault()
      setHighlight((h) => Math.max(h - 1, 0))
    } else if (e.key === 'Enter' && open) {
      e.preventDefault()
      const a = options[highlight]
      if (a) choose(a)
    } else if (e.key === 'Escape' && open) {
      e.stopPropagation()
      close()
    }
  }

  return (
    <div className="relative flex flex-col">
      <input
        id={id}
        type="text"
        role="combobox"
        aria-expanded={open}
        aria-controls={listId}
        aria-autocomplete="list"
        aria-activedescendant={open && options[highlight] ? `${listId}-${options[highlight].number}` : undefined}
        autoComplete="off"
        placeholder="Konto wählen…"
        className={`${className ?? ''} ${selected && query === null ? 'pr-24' : ''}`}
        value={text}
        onFocus={(e) => {
          setOpen(true)
          e.target.select()
        }}
        onChange={(e) => {
          setQuery(e.target.value)
          setHighlight(0)
          setOpen(true)
          if (e.target.value.trim() === '') onChange(null)
        }}
        onBlur={close}
        onKeyDown={onKeyDown}
      />
      {selected && query === null && (
        <span
          className={`pointer-events-none absolute right-2 top-1/2 -translate-y-1/2 rounded px-1.5 text-xs ${categoryStyle[selected.category]}`}
        >
          {categoryLabel[selected.category]}
        </span>
      )}
      {open && (
        <ul
          id={listId}
          role="listbox"
          className="absolute left-0 right-0 top-full z-10 mt-1 max-h-64 overflow-auto rounded border border-slate-300 bg-white shadow"
        >
          {options.length === 0 && <li className="p-2 text-slate-500">Kein Konto gefunden</li>}
          {options.map((a, i) => (
            <li
              key={a.number}
              id={`${listId}-${a.number}`}
              role="option"
              aria-selected={a.number === value}
              aria-disabled={!a.active}
              // mousedown (not click) so it fires before the input's blur closes the list
              onMouseDown={(e) => {
                e.preventDefault()
                choose(a)
              }}
              onMouseEnter={() => setHighlight(i)}
              className={`flex items-center justify-between gap-2 px-2 py-1 ${
                i === highlight ? 'bg-sky-100' : ''
              } ${a.active ? 'cursor-pointer' : 'cursor-not-allowed text-slate-400'}`}
            >
              <span>
                {accountLabel(a)}
                {a.active ? '' : ' (inaktiv)'}
              </span>
              <span className={`rounded px-1.5 text-xs ${categoryStyle[a.category]}`}>
                {categoryLabel[a.category]}
              </span>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

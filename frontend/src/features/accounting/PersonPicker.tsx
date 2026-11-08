import { useSuspenseQuery } from '@tanstack/react-query'
import { useId, useState } from 'react'
import { personsQueryOptions } from '../persons/api'
import { filterPersons, personLabel } from './personFilter'
import type { Person } from '../persons/types'

type Props = {
  value: number | null
  onChange: (id: number | null) => void
  className?: string
}

/** Person autocomplete: type part of a name to filter. Persons who have left are not offered. */
export function PersonPicker({ value, onChange, className }: Props) {
  const { data: persons } = useSuspenseQuery(personsQueryOptions)
  const listId = useId()
  const [query, setQuery] = useState<string | null>(null) // null = not editing
  const [open, setOpen] = useState(false)
  const [highlight, setHighlight] = useState(0)

  const selected = persons.find((p) => p.id === value) ?? null
  const text = query ?? (selected ? personLabel(selected) : '')
  const options = filterPersons(persons, query ?? '', value)

  const close = () => {
    setOpen(false)
    setQuery(null)
  }

  const choose = (p: Person) => {
    onChange(p.id)
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
      const p = options[highlight]
      if (p) choose(p)
    } else if (e.key === 'Escape' && open) {
      e.stopPropagation()
      close()
    }
  }

  return (
    <div className="relative flex flex-col">
      <input
        type="text"
        role="combobox"
        aria-expanded={open}
        aria-controls={listId}
        aria-autocomplete="list"
        aria-activedescendant={open && options[highlight] ? `${listId}-${options[highlight].id}` : undefined}
        autoComplete="off"
        placeholder="Person wählen…"
        className={className}
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
      {open && (
        <ul
          id={listId}
          role="listbox"
          className="absolute left-0 right-0 top-full z-10 mt-1 max-h-64 overflow-auto rounded border border-slate-300 bg-white shadow"
        >
          {options.length === 0 && <li className="p-2 text-slate-500">Keine Person gefunden</li>}
          {options.map((p, i) => (
            <li
              key={p.id}
              id={`${listId}-${p.id}`}
              role="option"
              aria-selected={p.id === value}
              // mousedown (not click) so it fires before the input's blur closes the list
              onMouseDown={(e) => {
                e.preventDefault()
                choose(p)
              }}
              onMouseEnter={() => setHighlight(i)}
              className={`cursor-pointer px-2 py-1 ${i === highlight ? 'bg-sky-100' : ''}`}
            >
              {personLabel(p)}
              {p.leftOn !== null ? ' (ausgetreten)' : ''}
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

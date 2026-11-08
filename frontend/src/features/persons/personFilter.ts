import type { Person } from './types'

export function personLabel(person: Person): string {
  return `${person.lastName} ${person.firstName}`
}

/**
 * Persons matching what the user typed (any part of first or last name, case-insensitive), sorted by last then
 * first name. Persons who have left (leftOn set) are only offered when they are the current value.
 */
export function filterPersons(persons: Person[], query: string, currentValue: number | null): Person[] {
  const words = query.trim().toLowerCase().split(/\s+/).filter(Boolean)
  return persons
    .filter((p) => p.leftOn === null || p.id === currentValue)
    .filter((p) => {
      const name = `${p.firstName} ${p.lastName}`.toLowerCase()
      return words.every((w) => name.includes(w))
    })
    .sort((a, b) => a.lastName.localeCompare(b.lastName, 'de') || a.firstName.localeCompare(b.firstName, 'de'))
}

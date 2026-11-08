import { describe, expect, it } from 'vitest'
import type { Person } from '../persons/types'
import { filterPersons } from './personFilter'

function person(id: number, firstName: string, lastName: string, leftOn: string | null = null): Person {
  return {
    id, firstName, lastName, leftOn,
    dateOfBirth: null, joinedOn: null, email: null, mobilePhone: null, landlinePhone: null,
    street: null, houseNumber: null, postalCode: null, city: null, country: null, notes: null,
  }
}

const persons = [person(1, 'Anna', 'Meier'), person(2, 'Beat', 'Alt', '2025-01-01'), person(3, 'Carla', 'Müller')]

describe('filterPersons', () => {
  it('hides persons who have left and sorts by last name', () => {
    expect(filterPersons(persons, '', null).map((p) => p.id)).toEqual([1, 3])
  })
  it('keeps a left person when they are the current value', () => {
    expect(filterPersons(persons, '', 2).map((p) => p.id)).toEqual([2, 1, 3])
  })
  it('matches parts of first and last name in any order', () => {
    expect(filterPersons(persons, 'meier an', null).map((p) => p.id)).toEqual([1])
    expect(filterPersons(persons, 'mü', null).map((p) => p.id)).toEqual([3])
  })
})

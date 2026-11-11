import type { MembershipType } from './membership'

export type Person = {
  id: number
  firstName: string
  lastName: string
  membership: MembershipType
  dateOfBirth: string | null
  joinedOn: string | null
  leftOn: string | null
  email: string | null
  mobilePhone: string | null
  landlinePhone: string | null
  street: string | null
  number: string | null
  postalCode: string | null
  city: string | null
  country: string | null
  notes: string | null
}

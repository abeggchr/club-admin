import {
  columnFilteringFeature,
  createColumnHelper,
  createFilteredRowModel,
  createPaginatedRowModel,
  createSortedRowModel,
  globalFilteringFeature,
  rowPaginationFeature,
  rowSortingFeature,
  sortFn_alphanumeric,
  tableFeatures,
} from '@tanstack/react-table'
import { formatDateDe, orDash } from './format'
import type { Person } from './types'

export const features = tableFeatures({
  columnFilteringFeature,
  globalFilteringFeature,
  rowPaginationFeature,
  rowSortingFeature,
  sortFns: {
    alphanumeric: sortFn_alphanumeric,
  },
  filteredRowModel: createFilteredRowModel(),
  paginatedRowModel: createPaginatedRowModel(),
  sortedRowModel: createSortedRowModel(),
})

export type Features = typeof features

const helper = createColumnHelper<Features, Person>()

export const columns = helper.columns([
  helper.accessor('firstName', { header: 'Vorname' }),
  helper.accessor('lastName', { header: 'Nachname' }),
  helper.accessor('dateOfBirth', {
    header: 'Geburtstag',
    cell: (info) => formatDateDe(info.getValue()),
  }),
  helper.accessor('joinedOn', {
    header: 'Eintritt',
    cell: (info) => formatDateDe(info.getValue()),
  }),
  helper.accessor('leftOn', {
    header: 'Austritt',
    cell: (info) => formatDateDe(info.getValue()),
  }),
  helper.accessor('email', {
    header: 'E-Mail',
    cell: (info) => {
      const email = info.getValue()
      return email ? (
        <a className="text-blue-600 hover:underline" href={`mailto:${email}`}>
          {email}
        </a>
      ) : orDash(email)
    },
  }),
  helper.accessor('mobilePhone', {
    header: 'Mobilfunknummer',
    cell: (info) => orDash(info.getValue()),
  }),
  helper.accessor('landlinePhone', {
    header: 'Festnetznummer',
    cell: (info) => orDash(info.getValue()),
  }),
  helper.accessor('street', {
    header: 'Strasse',
    cell: (info) => orDash(info.getValue()),
  }),
  helper.accessor('number', {
    header: 'Hausnummer',
    cell: (info) => orDash(info.getValue()),
  }),
  helper.accessor('postalCode', {
    header: 'PLZ',
    cell: (info) => orDash(info.getValue()),
  }),
  helper.accessor('city', {
    header: 'Ort',
    cell: (info) => orDash(info.getValue()),
  }),
  helper.accessor('country', {
    header: 'Land',
    cell: (info) => orDash(info.getValue()),
  }),
  helper.accessor('notes', {
    header: 'Notizen',
    cell: (info) => orDash(info.getValue()),
  }),
])

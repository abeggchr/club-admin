import { formatDe } from './format'
import { membershipLabels } from './membership'
import type { Person } from './types'

// Same headers and date format as the backend's PersonCsvImporter, so an
// exported file can be re-imported unchanged.
const exportColumns: { header: string; value: (p: Person) => string | null }[] = [
  { header: 'Vorname', value: (p) => p.firstName },
  { header: 'Nachname', value: (p) => p.lastName },
  { header: 'Mitgliedschaft', value: (p) => membershipLabels[p.membership] },
  { header: 'Geburtstag', value: (p) => formatDe(p.dateOfBirth) },
  { header: 'Eintritt', value: (p) => formatDe(p.joinedOn) },
  { header: 'Austritt', value: (p) => formatDe(p.leftOn) },
  { header: 'E-Mail', value: (p) => p.email },
  { header: 'Mobilfunknummer', value: (p) => p.mobilePhone },
  { header: 'Festnetznummer', value: (p) => p.landlinePhone },
  { header: 'Strasse', value: (p) => p.street },
  { header: 'Hausnummer', value: (p) => p.number },
  { header: 'PLZ', value: (p) => p.postalCode },
  { header: 'Ort', value: (p) => p.city },
  { header: 'Land', value: (p) => p.country },
  { header: 'Notizen', value: (p) => p.notes },
]

function buildPersonCsv(persons: Person[]): string {
  const esc = (v: string | null) => {
    const s = v ?? ''
    return /[";\n\r]/.test(s) ? `"${s.replaceAll('"', '""')}"` : s
  }
  return [
    exportColumns.map((c) => c.header).join(';'),
    ...persons.map((p) => exportColumns.map((c) => esc(c.value(p))).join(';')),
  ].join('\r\n')
}

export function downloadPersonCsv(persons: Person[]) {
  // BOM so Excel opens the UTF-8 file with the correct encoding.
  const url = URL.createObjectURL(new Blob([`﻿${buildPersonCsv(persons)}`], {
    type: 'text/csv;charset=utf-8',
  }))
  const a = Object.assign(document.createElement('a'), { href: url, download: 'personen.csv' })
  a.click()
  URL.revokeObjectURL(url)
}

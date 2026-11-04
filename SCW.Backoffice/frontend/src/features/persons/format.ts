export function orDash(value: string | null | undefined) {
  return value ?? '—'
}

export function formatDe(value: string | null): string {
  if (!value) return ''
  const [year, month, day] = value.split('-')
  return `${day}.${month}.${year}`
}

export function formatDateDe(value: string | null): string {
  return value ? formatDe(value) : '—'
}

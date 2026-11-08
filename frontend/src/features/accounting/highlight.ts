import { useEffect, useRef } from 'react'

/** Selected rows get a stronger tint than highlighted ones. */
export function rowTint(selected: boolean, highlighted: boolean): string {
  if (selected) return 'bg-sky-200 hover:bg-sky-300'
  if (highlighted) return 'bg-sky-50 hover:bg-sky-100'
  return 'hover:bg-slate-50'
}

/** Scrolls the first highlighted row (data-highlighted="true") of the returned container into view when the highlight changes. */
export function useScrollHighlightIntoView(highlightedIds: number[]) {
  const ref = useRef<HTMLDivElement>(null)
  const key = highlightedIds.join(',')
  useEffect(() => {
    if (key === '') return
    ref.current?.querySelector('[data-highlighted="true"]')?.scrollIntoView({ block: 'nearest' })
  }, [key])
  return ref
}

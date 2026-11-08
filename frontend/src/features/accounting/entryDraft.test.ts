import { describe, expect, it } from 'vitest'
import { isEntryDraftDirty, type EntryDraft } from './entryDraft'

const initial: EntryDraft = { date: '2026-11-08', text: '', accountNumber: null, amount: '' }

describe('isEntryDraftDirty', () => {
  it('is clean when nothing changed', () => {
    expect(isEntryDraftDirty(initial, { ...initial })).toBe(false)
  })

  it.each([
    ['date', { date: '2026-11-09' }],
    ['text', { text: 'Material' }],
    ['account', { accountNumber: 4000 }],
    ['amount', { amount: '12.50' }],
  ])('is dirty when the %s changed', (_field, change) => {
    expect(isEntryDraftDirty(initial, { ...initial, ...change })).toBe(true)
  })

  it('is clean again when a change is reverted', () => {
    const edited = { ...initial, text: 'Material' }
    expect(isEntryDraftDirty(initial, { ...edited, text: '' })).toBe(false)
  })
})

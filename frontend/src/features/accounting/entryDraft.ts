/** The Buchung form's field values as the user sees them (amount is the typed text). */
export type EntryDraft = {
  date: string
  text: string
  accountNumber: number | null
  amount: string
}

/** True when the user has changed any field away from where the form started. */
export function isEntryDraftDirty(initial: EntryDraft, current: EntryDraft): boolean {
  return (
    initial.date !== current.date ||
    initial.text !== current.text ||
    initial.accountNumber !== current.accountNumber ||
    initial.amount !== current.amount
  )
}

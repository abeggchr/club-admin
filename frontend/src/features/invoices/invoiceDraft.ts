import { parseAmount } from '../accounting/amount'

export type InvoiceLineRow = { text: string; amount: string; account: number | null }

/** The Rechnung form's field values as the user sees them. */
export type InvoiceDraft = {
  personId: number | null
  templateId: number | null
  lines: InvoiceLineRow[]
  /** "Betrag offen lassen": the QR code carries no amount. */
  openAmount: boolean
}

export type InvoiceDraftErrors = {
  person?: string
  template?: string
  lines?: string
  rows: { text?: string; amount?: string; account?: string }[]
}

export type InvoiceInput = {
  personId: number
  templateId: number
  lines: { text: string; amount: number; accountNumber: number }[]
  openAmount: boolean
}

const emptyLine = (): InvoiceLineRow => ({ text: '', amount: '', account: null })

export function emptyInvoiceDraft(): InvoiceDraft {
  return { personId: null, templateId: null, lines: [emptyLine()], openAmount: false }
}

export function addLine(draft: InvoiceDraft): InvoiceDraft {
  return { ...draft, lines: [...draft.lines, emptyLine()] }
}

export function removeLine(draft: InvoiceDraft, index: number): InvoiceDraft {
  return { ...draft, lines: draft.lines.filter((_, i) => i !== index) }
}

/** The sum of all positions with a valid amount (cents-exact); invalid or empty amounts count as 0. */
export function totalOf(lines: InvoiceLineRow[]): number {
  const cents = lines.reduce((sum, line) => {
    const parsed = parseAmount(line.amount)
    return parsed.ok && parsed.value > 0 ? sum + Math.round(parsed.value * 100) : sum
  }, 0)
  return cents / 100
}

/** The form's validation errors, or null when the draft can be saved. */
export function validateInvoiceDraft(draft: InvoiceDraft): InvoiceDraftErrors | null {
  const errors: InvoiceDraftErrors = { rows: [] }
  let any = false
  if (draft.personId === null) {
    errors.person = 'Person ist erforderlich.'
    any = true
  }
  if (draft.templateId === null) {
    errors.template = 'Textvorlage ist erforderlich.'
    any = true
  }
  if (draft.lines.length === 0) {
    errors.lines = 'Mindestens eine Position ist erforderlich.'
    any = true
  }
  for (const line of draft.lines) {
    const row: { text?: string; amount?: string; account?: string } = {}
    if (!line.text.trim()) row.text = 'Text ist erforderlich.'
    const parsed = parseAmount(line.amount, { allowZero: true })
    if (!parsed.ok) row.amount = parsed.error
    else if (parsed.value <= 0) row.amount = 'Betrag muss grösser als 0 sein.'
    if (line.account === null) row.account = 'Konto ist erforderlich.'
    if (row.text || row.amount || row.account) any = true
    errors.rows.push(row)
  }
  return any ? errors : null
}

/** The request body for POST /api/invoices, or null while the draft is invalid. */
export function toInvoiceInput(draft: InvoiceDraft): InvoiceInput | null {
  if (validateInvoiceDraft(draft) || draft.personId === null || draft.templateId === null) return null
  const lines: InvoiceInput['lines'] = []
  for (const line of draft.lines) {
    if (line.account === null) return null
    const parsed = parseAmount(line.amount)
    lines.push({ text: line.text.trim(), amount: parsed.ok ? parsed.value : 0, accountNumber: line.account })
  }
  return {
    personId: draft.personId,
    templateId: draft.templateId,
    lines,
    openAmount: draft.openAmount,
  }
}

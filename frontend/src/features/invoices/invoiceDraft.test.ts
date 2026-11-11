import { describe, expect, it } from 'vitest'
import {
  addLine,
  emptyInvoiceDraft,
  removeLine,
  toInvoiceInput,
  totalOf,
  validateInvoiceDraft,
  type InvoiceDraft,
} from './invoiceDraft'

function filled(overrides: Partial<InvoiceDraft> = {}): InvoiceDraft {
  return {
    personId: 7,
    templateId: 3,
    lines: [{ text: 'Jahresbeitrag', amount: '120', account: 3400 }],
    openAmount: false,
    ...overrides,
  }
}

describe('validateInvoiceDraft', () => {
  it('accepts a draft with Person, Textvorlage and one valid position', () => {
    expect(validateInvoiceDraft(filled())).toBeNull()
  })

  it('refuses a draft without Person', () => {
    expect(validateInvoiceDraft(filled({ personId: null }))?.person).toBe('Person ist erforderlich.')
  })

  it('refuses a draft without Textvorlage', () => {
    expect(validateInvoiceDraft(filled({ templateId: null }))?.template).toBe('Textvorlage ist erforderlich.')
  })

  it('refuses a draft without positions', () => {
    expect(validateInvoiceDraft(filled({ lines: [] }))?.lines).toBe('Mindestens eine Position ist erforderlich.')
  })

  it('refuses a position with an empty text', () => {
    const errors = validateInvoiceDraft(filled({ lines: [{ text: '  ', amount: '10', account: 3400 }] }))
    expect(errors?.rows[0].text).toBe('Text ist erforderlich.')
  })

  it('accepts a negative position while the total stays positive', () => {
    const lines = [
      { text: 'a', amount: '100', account: 3400 },
      { text: 'b', amount: '-30', account: 3400 },
    ]
    expect(validateInvoiceDraft(filled({ lines }))).toBeNull()
    expect(totalOf(lines)).toBe(70)
  })

  it('refuses a total of 0 or less', () => {
    const lines = [
      { text: 'a', amount: '10', account: 3400 },
      { text: 'b', amount: '-30', account: 3400 },
    ]
    expect(validateInvoiceDraft(filled({ lines }))?.lines).toBe('Die Summe der Positionen muss grösser als 0 sein.')
  })

  it('refuses a position with an amount of 0', () => {
    expect(
      validateInvoiceDraft(filled({ lines: [{ text: 'a', amount: '0', account: 3400 }] }))?.rows[0].amount,
    ).toBeDefined()
  })

  it('refuses a position with an unparsable amount', () => {
    expect(
      validateInvoiceDraft(filled({ lines: [{ text: 'a', amount: 'abc', account: 3400 }] }))?.rows[0].amount,
    ).toBe('Ungültiger Betrag.')
  })

  it('refuses a position without a Konto', () => {
    const errors = validateInvoiceDraft(filled({ lines: [{ text: 'a', amount: '1', account: null }] }))
    expect(errors?.rows[0].account).toBe('Konto ist erforderlich.')
  })

  it('reports errors per position', () => {
    const errors = validateInvoiceDraft(
      filled({
        lines: [
          { text: 'ok', amount: '1', account: 3400 },
          { text: '', amount: '1', account: null },
        ],
      }),
    )
    expect(errors?.rows[0]).toEqual({})
    expect(errors?.rows[1].text).toBeDefined()
    expect(errors?.rows[1].account).toBeDefined()
  })
})

describe('totalOf', () => {
  it('sums the amounts exactly in cents', () => {
    const lines = [
      { text: 'a', amount: '0.10', account: 3400 },
      { text: 'b', amount: "1'000.20", account: 3400 },
    ]
    expect(totalOf(lines)).toBe(1000.3)
  })

  it('ignores positions whose amount is not (yet) valid', () => {
    expect(
      totalOf([
        { text: 'a', amount: '10', account: 3400 },
        { text: 'b', amount: 'x', account: 3400 },
        { text: 'c', amount: '', account: null },
      ]),
    ).toBe(10)
  })

  it('is 0 without positions', () => {
    expect(totalOf([])).toBe(0)
  })
})

describe('addLine / removeLine', () => {
  it('starts with one empty position', () => {
    expect(emptyInvoiceDraft().lines).toEqual([{ text: '', amount: '', account: null }])
  })

  it('appends an empty position', () => {
    expect(addLine(filled()).lines).toHaveLength(2)
    expect(addLine(filled()).lines[1]).toEqual({ text: '', amount: '', account: null })
  })

  it('removes the position at the index', () => {
    const draft = filled({
      lines: [
        { text: 'a', amount: '1', account: 3400 },
        { text: 'b', amount: '2', account: 3400 },
      ],
    })
    expect(removeLine(draft, 0).lines).toEqual([{ text: 'b', amount: '2', account: 3400 }])
  })
})

describe('toInvoiceInput', () => {
  it('builds the request body with parsed amounts', () => {
    expect(
      toInvoiceInput(filled({ lines: [{ text: ' Beitrag ', amount: '1’250.5', account: 3400 }] })),
    ).toEqual({
      personId: 7,
      templateId: 3,
      lines: [{ text: 'Beitrag', amount: 1250.5, accountNumber: 3400 }],
      openAmount: false,
    })
  })

  it('is not open by default', () => {
    expect(emptyInvoiceDraft().openAmount).toBe(false)
  })

  it('passes the open-amount flag on', () => {
    expect(toInvoiceInput(filled({ openAmount: true }))?.openAmount).toBe(true)
  })

  it('is null for an invalid draft', () => {
    expect(toInvoiceInput(filled({ personId: null }))).toBeNull()
  })
})

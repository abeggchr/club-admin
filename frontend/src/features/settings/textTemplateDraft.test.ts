import { describe, expect, it } from 'vitest'
import { emptyTextTemplateDraft, validateTextTemplate } from './textTemplateDraft'

describe('validateTextTemplate', () => {
  const filled = { title: 'Mitgliederbeitrag', text: 'Wir stellen in Rechnung:', greetings: 'Freundliche Grüsse' }

  it('accepts a template with all three fields', () => {
    expect(validateTextTemplate(filled)).toBeNull()
  })

  it('rejects an empty draft', () => {
    expect(validateTextTemplate(emptyTextTemplateDraft)).toBe('Titel ist erforderlich.')
  })

  it('rejects a blank text', () => {
    expect(validateTextTemplate({ ...filled, text: '  ' })).toBe('Text ist erforderlich.')
  })

  it('rejects blank greetings', () => {
    expect(validateTextTemplate({ ...filled, greetings: '' })).toBe('Grüsse sind erforderlich.')
  })
})

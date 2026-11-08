export type TextTemplateDraft = { title: string; text: string; greetings: string }

export const emptyTextTemplateDraft: TextTemplateDraft = { title: '', text: '', greetings: '' }

/** German error message, or null when all three fields are filled. */
export function validateTextTemplate(draft: TextTemplateDraft): string | null {
  if (!draft.title.trim()) return 'Titel ist erforderlich.'
  if (!draft.text.trim()) return 'Text ist erforderlich.'
  if (!draft.greetings.trim()) return 'Grüsse sind erforderlich.'
  return null
}

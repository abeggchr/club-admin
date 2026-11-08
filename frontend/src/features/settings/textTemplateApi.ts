import { queryOptions } from '@tanstack/react-query'
import { ensureOk, getJson, requestJson } from '../accounting/api'
import type { TextTemplateDraft } from './textTemplateDraft'

export type TextTemplate = TextTemplateDraft & { id: number }

export const textTemplatesQueryOptions = queryOptions({
  queryKey: ['text-templates'],
  queryFn: () => getJson<TextTemplate[]>('/api/text-templates'),
})

/** Creates a Textvorlage (id null) or replaces an existing one. */
export function saveTextTemplate(id: number | null, draft: TextTemplateDraft): Promise<TextTemplate> {
  return requestJson(id === null ? '/api/text-templates' : `/api/text-templates/${id}`, id === null ? 'POST' : 'PUT', draft)
}

export async function deleteTextTemplate(id: number): Promise<void> {
  await ensureOk(await fetch(`/api/text-templates/${id}`, { method: 'DELETE' }))
}

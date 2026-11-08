import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { FiX } from 'react-icons/fi'
import { personsQueryOptions, updatePerson } from './api'
import type { Person } from './types'

const textFields: { key: keyof Person; label: string; type?: string; multiline?: boolean }[] = [
  { key: 'firstName', label: 'Vorname' },
  { key: 'lastName', label: 'Nachname' },
  { key: 'dateOfBirth', label: 'Geburtstag', type: 'date' },
  { key: 'joinedOn', label: 'Eintritt', type: 'date' },
  { key: 'leftOn', label: 'Austritt', type: 'date' },
  { key: 'email', label: 'E-Mail', type: 'email' },
  { key: 'mobilePhone', label: 'Mobilfunknummer', type: 'tel' },
  { key: 'landlinePhone', label: 'Festnetznummer', type: 'tel' },
  { key: 'street', label: 'Strasse' },
  { key: 'houseNumber', label: 'Hausnummer' },
  { key: 'postalCode', label: 'PLZ' },
  { key: 'city', label: 'Ort' },
  { key: 'country', label: 'Land' },
  { key: 'notes', label: 'Notizen', multiline: true },
]

export function EditPanel({ person, onClose }: { person: Person; onClose: () => void }) {
  const queryClient = useQueryClient()
  const [draft, setDraft] = useState(person)
  const [error, setError] = useState<string | null>(null)
  const [saved, setSaved] = useState(false)
  const update = useMutation({
    mutationFn: updatePerson,
    onSuccess: (updated) => {
      setDraft(updated)
      setSaved(true)
      return queryClient.invalidateQueries({ queryKey: personsQueryOptions.queryKey })
    },
    onError: (err) => setError(err.message),
  })
  const busy = update.isPending

  function submit(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    setSaved(false)
    // Empty inputs are stored as null, matching the importer.
    const payload = Object.fromEntries(
      Object.entries(draft).map(([k, v]) => [k, v === '' ? null : v]),
    ) as Person
    update.mutate(payload)
  }

  return (
    <aside className="w-96 shrink-0 rounded border border-neutral-200 p-4">
      <form onSubmit={submit}>
        <div className="flex items-center justify-between">
          <h2 className="text-xl font-bold">Person bearbeiten</h2>
          <button type="button" onClick={onClose} aria-label="Abbrechen" className="p-1">
            <FiX />
          </button>
        </div>
        {textFields.map((f) => {
          const common = {
            id: `person-${f.key}`,
            value: (draft[f.key] as string | null) ?? '',
            required: f.key === 'firstName' || f.key === 'lastName',
            onChange: (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
              setDraft({ ...draft, [f.key]: e.target.value })
              setSaved(false)
            },
            className: 'mt-1 block w-full rounded border border-neutral-300 px-2 py-1 text-sm',
          }
          return (
            <div key={f.key} className="mt-3">
              <label htmlFor={common.id} className="text-sm font-medium">
                {f.label}
              </label>
              {f.multiline ? (
                <textarea rows={4} {...common} />
              ) : (
                <input type={f.type ?? 'text'} {...common} />
              )}
            </div>
          )
        })}
        {error && <p className="mt-3 text-sm text-red-600">{error}</p>}
        {saved && <p className="mt-3 text-sm text-green-700">Gespeichert.</p>}
        <div className="mt-4 flex justify-end gap-2">
          <button type="button" onClick={onClose} className="rounded border border-slate-300 hover:bg-sky-100 px-4 py-2">
            Abbrechen
          </button>
          <button
            type="submit"
            disabled={busy}
            className="rounded bg-sky-600 px-4 py-2 text-white enabled:hover:bg-sky-700 disabled:opacity-40"
          >
            {busy ? 'Speichere…' : 'Speichern'}
          </button>
        </div>
      </form>
    </aside>
  )
}

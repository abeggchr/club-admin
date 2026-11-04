import { createFileRoute } from '@tanstack/react-router'

type Person = {
  id: string,
  firstName: string
  lastName: string
  email: string
}

async function fetchPersons(): Promise<Person[]> {
  const res = await fetch('/api/persons')
  if (!res.ok) throw new Error(`Backend returned ${res.status}`)
  return res.json()
}

export const Route = createFileRoute('/persons')({
  loader: fetchPersons,
  component: Persons,
  pendingComponent: () => <p className="p-8">Loading…</p>,
  errorComponent: ({ error }) => (
    <p className="p-8 text-red-600">Failed to load: {error.message}</p>
  ),
})

function Persons() {
  const forecasts = Route.useLoaderData()

  return (
    <div className="p-8">
      <h1 className="text-3xl font-bold">Persons</h1>
      <table className="mt-4 text-left">
        <thead>
        <tr>
          <th className="pr-6">Vorname</th>
          <th className="pr-6">Nachname</th>
          <th className="pr-6">Email</th>
        </tr>
        </thead>
        <tbody>
        {forecasts.map((person: Person) => (
          <tr key={person.id}>
            <td className="pr-6">{person.firstName}</td>
            <td className="pr-6">{person.lastName}</td>
            <td className="pr-6">{person.email}</td>
          </tr>
        ))}
        </tbody>
      </table>
    </div>
  )
}

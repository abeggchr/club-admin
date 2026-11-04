import { createFileRoute } from '@tanstack/react-router'

type Forecast = {
  date: string
  temperatureC: number
  temperatureF: number
  summary: string | null
}

async function fetchForecasts(): Promise<Forecast[]> {
  const res = await fetch('/api/weatherforecast')
  if (!res.ok) throw new Error(`Backend returned ${res.status}`)
  return res.json()
}

export const Route = createFileRoute('/weather')({
  loader: fetchForecasts,
  component: Weather,
  pendingComponent: () => <p className="p-8">Loading…</p>,
  errorComponent: ({ error }) => (
    <p className="p-8 text-red-600">Failed to load: {error.message}</p>
  ),
})

function Weather() {
  const forecasts = Route.useLoaderData()

  return (
    <div className="p-8">
      <h1 className="text-3xl font-bold">Weather forecast</h1>
      <table className="mt-4 text-left">
        <thead>
          <tr>
            <th className="pr-6">Date</th>
            <th className="pr-6">°C</th>
            <th className="pr-6">°F</th>
            <th>Summary</th>
          </tr>
        </thead>
        <tbody>
          {forecasts.map((f) => (
            <tr key={f.date}>
              <td className="pr-6">{f.date}</td>
              <td className="pr-6">{f.temperatureC}</td>
              <td className="pr-6">{f.temperatureF}</td>
              <td>{f.summary}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

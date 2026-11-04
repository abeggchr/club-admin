import { MdPeople } from 'react-icons/md'
import { Link, Outlet, createRootRouteWithContext } from '@tanstack/react-router'
import type { QueryClient } from '@tanstack/react-query'
import { ReactQueryDevtoolsPanel } from '@tanstack/react-query-devtools'

import { TanStackRouterDevtoolsPanel } from '@tanstack/react-router-devtools'
import { TanStackDevtools } from '@tanstack/react-devtools'

import '../styles.css'

export const Route = createRootRouteWithContext<{ queryClient: QueryClient }>()({
  component: RootComponent,
})

const navItems = [
  { to: '/persons', label: 'Persons', Icon: MdPeople },
] as const

function RootComponent() {
  return (
    <>
      <header className="px-4 pt-4">
        <h1 className="text-2xl font-bold text-sky-900">Skiclub Waidberg</h1>
        <nav className="mt-2 flex gap-1 rounded-lg bg-sky-500 p-2 shadow">
          {navItems.map((item) => (
            <Link
              key={item.to}
              to={item.to}
              title={item.label}
              aria-label={item.label}
              className="rounded-md p-2 text-sky-100 transition-colors hover:bg-sky-400 hover:text-white"
              activeProps={{ className: 'bg-sky-700 text-white' }}
            >
              <item.Icon className="size-6" />
            </Link>
          ))}
        </nav>
      </header>
      <Outlet />
      <TanStackDevtools
        config={{
          position: 'bottom-right',
        }}
        plugins={[
          {
            name: 'TanStack Router',
            render: <TanStackRouterDevtoolsPanel />,
          },
          {
            name: 'TanStack Query',
            render: <ReactQueryDevtoolsPanel />,
          },
        ]}
      />
    </>
  )
}

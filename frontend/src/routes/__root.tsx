import { TanStackDevtools } from "@tanstack/react-devtools";
import type { QueryClient } from "@tanstack/react-query";
import { useQuery } from "@tanstack/react-query";
import { ReactQueryDevtoolsPanel } from "@tanstack/react-query-devtools";
import {
	createRootRouteWithContext,
	Link,
	Outlet,
} from "@tanstack/react-router";
import { TanStackRouterDevtoolsPanel } from "@tanstack/react-router-devtools";
import { settingsQueryOptions } from "../features/accounting/api";
import { fetchCurrentUser, logout } from "../lib/auth";
import { navItems } from "../nav";

import "../styles.css";

export const Route = createRootRouteWithContext<{
	queryClient: QueryClient;
}>()({
	beforeLoad: async () => ({ user: await fetchCurrentUser() }),
	component: RootComponent,
});

function RootComponent() {
	const { data: settings } = useQuery(settingsQueryOptions);
	const { user } = Route.useRouteContext();

	return (
		<>
			<header className="px-4 pt-4">
				<div className="flex items-baseline justify-between">
					<h1 className="text-2xl font-bold text-sky-900">
						<Link to="/" aria-label="Übersicht">
							{settings?.name.trim() || "Admin"}
						</Link>
					</h1>
					<div className="flex items-center gap-3 text-sm text-sky-900">
						<span>{user.email ?? user.name}</span>
						<button
							type="button"
							onClick={() => void logout()}
							className="rounded-md px-2 py-1 hover:bg-sky-100"
						>
							Abmelden
						</button>
					</div>
				</div>
				<nav className="mt-2 flex gap-1 rounded-lg bg-sky-500 p-2 shadow">
					{navItems.map((item) => (
						<Link
							key={item.to}
							to={item.to}
							title={item.label}
							aria-label={item.label}
							className={`rounded-md p-2 text-sky-100 transition-colors hover:bg-sky-400 hover:text-white ${
								item.align === "end" ? "ml-auto" : ""
							}`}
							activeProps={{ className: "bg-sky-700 text-white" }}
						>
							<item.Icon className="size-6" />
						</Link>
					))}
				</nav>
			</header>
			<Outlet />
			<TanStackDevtools
				config={{
					position: "bottom-right",
				}}
				plugins={[
					{
						name: "TanStack Router",
						render: <TanStackRouterDevtoolsPanel />,
					},
					{
						name: "TanStack Query",
						render: <ReactQueryDevtoolsPanel />,
					},
				]}
			/>
		</>
	);
}

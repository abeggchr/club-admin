import {
	createFileRoute,
	type ErrorComponentProps,
} from "@tanstack/react-router";
import { personsQueryOptions } from "../features/persons/api";
import { PersonsPage } from "../features/persons/PersonsPage";

export const Route = createFileRoute("/persons")({
	validateSearch: (search: Record<string, unknown>): { person?: number } => {
		const n = Number(search.person);
		return Number.isInteger(n) && n > 0 ? { person: n } : {};
	},
	loader: ({ context: { queryClient } }) =>
		queryClient.ensureQueryData(personsQueryOptions),
	component: PersonsPage,
	pendingComponent: () => <p className="p-8">Loading…</p>,
	errorComponent: (error: ErrorComponentProps) => (
		<p className="p-8 text-red-600">Failed to load: {JSON.stringify(error)}</p>
	),
});

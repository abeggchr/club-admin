import {
	createFileRoute,
	type ErrorComponentProps,
} from "@tanstack/react-router";
import {
	invoicesSummaryQueryOptions,
	personsSummaryQueryOptions,
} from "../features/dashboard/api";
import { Dashboard } from "../features/dashboard/Dashboard";
import { jubileesQueryOptions } from "../features/jubilees/api";

export const Route = createFileRoute("/")({
	loader: ({ context: { queryClient } }) =>
		Promise.all([
			queryClient.ensureQueryData(jubileesQueryOptions),
			queryClient.ensureQueryData(personsSummaryQueryOptions),
			queryClient.ensureQueryData(invoicesSummaryQueryOptions),
		]),
	component: Dashboard,
	pendingComponent: () => <p className="p-8">Laden…</p>,
	errorComponent: ({ error }: ErrorComponentProps) => (
		<p className="p-8 text-red-600">
			Laden fehlgeschlagen:{" "}
			{error instanceof Error ? error.message : String(error)}
		</p>
	),
});

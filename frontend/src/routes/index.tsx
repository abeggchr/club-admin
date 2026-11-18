import {
	createFileRoute,
	type ErrorComponentProps,
	redirect,
} from "@tanstack/react-router";
import { settingsQueryOptions } from "../features/accounting/api";
import {
	invoicesSummaryQueryOptions,
	personsSummaryQueryOptions,
} from "../features/dashboard/api";
import { Dashboard } from "../features/dashboard/Dashboard";
import { jubileesQueryOptions } from "../features/jubilees/api";

export const Route = createFileRoute("/")({
	beforeLoad: async ({ context: { queryClient } }) => {
		if (!(await queryClient.ensureQueryData(settingsQueryOptions))) {
			throw redirect({ to: "/settings" });
		}
	},
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

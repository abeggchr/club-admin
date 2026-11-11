import {
	createFileRoute,
	type ErrorComponentProps,
} from "@tanstack/react-router";
import { AccountingPage } from "../features/accounting/AccountingPage";
import { settingsQueryOptions } from "../features/accounting/api";

export const Route = createFileRoute("/accounting")({
	loader: ({ context: { queryClient } }) =>
		queryClient.ensureQueryData(settingsQueryOptions),
	component: AccountingPage,
	pendingComponent: () => <p className="p-8">Laden…</p>,
	errorComponent: ({ error }: ErrorComponentProps) => (
		<p className="p-8 text-red-600">
			Laden fehlgeschlagen:{" "}
			{error instanceof Error ? error.message : String(error)}
		</p>
	),
});

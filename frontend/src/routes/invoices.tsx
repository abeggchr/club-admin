import {
	createFileRoute,
	type ErrorComponentProps,
} from "@tanstack/react-router";
import { settingsQueryOptions } from "../features/accounting/api";
import { invoicesQueryOptions } from "../features/invoices/api";
import { InvoicesPage } from "../features/invoices/InvoicesPage";
import { personsQueryOptions } from "../features/persons/api";
import { textTemplatesQueryOptions } from "../features/settings/textTemplateApi";

export const Route = createFileRoute("/invoices")({
	loader: ({ context: { queryClient } }) =>
		Promise.all([
			queryClient.ensureQueryData(invoicesQueryOptions),
			queryClient.ensureQueryData(personsQueryOptions),
			queryClient.ensureQueryData(textTemplatesQueryOptions),
			queryClient.ensureQueryData(settingsQueryOptions),
		]),
	component: InvoicesPage,
	pendingComponent: () => <p className="p-8">Laden…</p>,
	errorComponent: ({ error }: ErrorComponentProps) => (
		<p className="p-8 text-red-600">
			Laden fehlgeschlagen:{" "}
			{error instanceof Error ? error.message : String(error)}
		</p>
	),
});

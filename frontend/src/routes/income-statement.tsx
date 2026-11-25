import {
	createFileRoute,
	type ErrorComponentProps,
} from "@tanstack/react-router";
import { incomeStatementQueryOptions } from "../features/income-statement/api";
import {
	IncomeStatementErrorComponent,
	IncomeStatementPage,
} from "../features/income-statement/IncomeStatementPage";

export const Route = createFileRoute("/income-statement")({
	loader: ({ context: { queryClient } }) =>
		queryClient.ensureQueryData(incomeStatementQueryOptions),
	component: IncomeStatementPage,
	pendingComponent: () => <p className="p-8">Laden…</p>,
	errorComponent: ({ error }: ErrorComponentProps) => (
		<IncomeStatementErrorComponent error={error} />
	),
});

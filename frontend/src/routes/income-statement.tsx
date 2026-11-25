import {
	createFileRoute,
	type ErrorComponentProps,
} from "@tanstack/react-router";
import { incomeStatementQueryOptions } from "../features/income-statement/api";
import {
	IncomeStatementErrorComponent,
	IncomeStatementPage,
} from "../features/income-statement/IncomeStatementPage";
import { parseIncomeStatementView } from "../features/income-statement/view";

export const Route = createFileRoute("/income-statement")({
	validateSearch: (search: Record<string, unknown>) => ({
		view: parseIncomeStatementView(search),
	}),
	loader: ({ context: { queryClient }, location }) =>
		queryClient.ensureQueryData(
			incomeStatementQueryOptions(parseIncomeStatementView(location.search)),
		),
	component: IncomeStatementPage,
	pendingComponent: () => <p className="p-8">Laden…</p>,
	errorComponent: ({ error }: ErrorComponentProps) => (
		<IncomeStatementErrorComponent error={error} />
	),
});

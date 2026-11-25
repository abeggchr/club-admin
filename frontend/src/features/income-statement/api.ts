import { queryOptions } from "@tanstack/react-query";
import { apiFetch } from "../../lib/apiFetch";
import type { IncomeStatement } from "./types";
import type { IncomeStatementView } from "./view";

/** The report is computed by the backend; a German error surfaces for a missing Start Date. */
async function fetchIncomeStatement(
	view: IncomeStatementView,
): Promise<IncomeStatement> {
	const res = await apiFetch(`/api/income-statement?view=${view}`);
	if (!res.ok) {
		const data = await res.json().catch(() => null);
		throw new Error(data?.error ?? `Backend returned ${res.status}`);
	}
	return res.json();
}

export const incomeStatementQueryOptions = (view: IncomeStatementView) =>
	queryOptions({
		queryKey: ["income-statement", view],
		queryFn: () => fetchIncomeStatement(view),
	});

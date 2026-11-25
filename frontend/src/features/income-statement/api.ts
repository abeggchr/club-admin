import { queryOptions } from "@tanstack/react-query";
import { apiFetch } from "../../lib/apiFetch";
import type { IncomeStatement } from "./types";

/** The report is computed by the backend; a German error surfaces for a missing Start Date. */
async function fetchIncomeStatement(): Promise<IncomeStatement> {
	const res = await apiFetch("/api/income-statement");
	if (!res.ok) {
		const data = await res.json().catch(() => null);
		throw new Error(data?.error ?? `Backend returned ${res.status}`);
	}
	return res.json();
}

export const incomeStatementQueryOptions = queryOptions({
	queryKey: ["income-statement"],
	queryFn: fetchIncomeStatement,
});

export type IncomeStatementView = "simplified" | "all";

/**
 * Parses the `view` URL param of the Income Statement page. Only "simplified" and "all"
 * are accepted; anything else (including a missing param) falls back to the default,
 * so a shared link never breaks on a stray or unknown value.
 */
export function parseIncomeStatementView(
	search: Record<string, unknown>,
): IncomeStatementView {
	const view = search.view;
	if (view === "simplified" || view === "all") {
		return view;
	}
	return "simplified";
}

export type IncomeStatementView = "simplified" | "all";

/**
 * Parses the `view` URL param of the Income Statement page. A missing or unknown value is a
 * user input error, not a fallback: the route's validateSearch throws the German error, which
 * TanStack Router shows through the route's errorComponent, like the API rejects the param.
 */
export function parseIncomeStatementView(
	search: Record<string, unknown>,
): IncomeStatementView {
	const raw = search.view;
	if (raw === undefined || raw === "") {
		return "simplified";
	}
	if (raw === "simplified" || raw === "all") {
		return raw;
	}
	throw new Error(
		`Unbekannte Ansicht '${String(raw)}'. Bitte 'simplified' oder 'all' verwenden.`,
	);
}

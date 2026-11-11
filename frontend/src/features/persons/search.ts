import { rankItem, rankings } from "@tanstack/match-sorter-utils";
import type { Row } from "@tanstack/react-table";
import type { Features } from "./columns";
import { membershipLabels } from "./membership";
import type { Person } from "./types";

export function globalSearchFilter(
	row: Row<Features, Person>,
	_columnId: string,
	filterValue: unknown,
) {
	const query = String(filterValue ?? "").trim();
	if (!query) return true;
	return (
		rankItem(row.original, query, {
			accessors: [
				(p) => p.firstName,
				(p) => p.lastName,
				(p) => membershipLabels[p.membership],
				(p) => p.email ?? "",
				(p) => p.city ?? "",
				(p) => p.country ?? "",
				(p) => p.notes ?? "",
			],
		}).rank > rankings.NO_MATCH
	);
}

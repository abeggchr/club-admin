import { useSuspenseQuery } from "@tanstack/react-query";
import { personsQueryOptions } from "../persons/api";
import { personLabel } from "../persons/personFilter";
import { formatChf } from "./amount";
import { rowTint, useScrollHighlightIntoView } from "./highlight";
import type { Transaction } from "./types";

type Props = {
	transactions: Transaction[];
	selectedId: number | null;
	highlightedIds: number[];
	onSelect: (id: number) => void;
};

/** The Bewegungen, newest first, each with the Kontostand after it. */
export function TransactionsTable({
	transactions,
	selectedId,
	highlightedIds,
	onSelect,
}: Props) {
	const { data: persons } = useSuspenseQuery(personsQueryOptions);
	const containerRef = useScrollHighlightIntoView(highlightedIds);
	return (
		<div
			ref={containerRef}
			className="min-w-0 flex-1 overflow-x-auto rounded border border-slate-200"
		>
			<table className="w-full text-left text-sm">
				<thead className="border-b border-slate-200 bg-slate-50">
					<tr>
						<th className="px-3 py-2 font-medium">Datum</th>
						<th className="px-3 py-2 font-medium">Person / Text</th>
						<th className="px-3 py-2 text-right font-medium">Betrag</th>
						<th className="px-3 py-2 text-right font-medium">Kontostand</th>
					</tr>
				</thead>
				<tbody>
					{transactions.length === 0 ? (
						<tr>
							<td colSpan={5} className="px-3 py-8 text-center text-slate-500">
								Noch keine Bewegungen.
							</td>
						</tr>
					) : (
						transactions.map((t) => {
							const selected = t.id === selectedId;
							const highlighted = highlightedIds.includes(t.id);
							const person = persons.find((p) => p.id === t.personId);
							return (
								<tr
									key={t.id}
									onClick={() => onSelect(t.id)}
									aria-selected={selected}
									data-highlighted={highlighted}
									className={`cursor-pointer border-b border-slate-100 last:border-0 ${rowTint(selected, highlighted)}`}
								>
									<td className="px-3 py-2">
										{new Date(t.date).toLocaleDateString("de-CH")}
									</td>
									<td className="px-3 py-2">
										{person ? personLabel(person) : (t.text ?? "")}
										{t.entryIds.length === 0 && (
											<span className="ml-2 whitespace-nowrap rounded bg-amber-100 px-1.5 text-xs text-amber-800">
												Keine Buchung
											</span>
										)}
									</td>
									<td
										className={`px-3 py-2 text-right tabular-nums ${t.amount < 0 ? "text-red-700" : ""}`}
									>
										{formatChf(t.amount)}
									</td>
									<td className="px-3 py-2 text-right tabular-nums">
										{formatChf(t.balance)}
									</td>
								</tr>
							);
						})
					)}
				</tbody>
			</table>
		</div>
	);
}

import { useSuspenseQuery } from "@tanstack/react-query";
import {
	createColumnHelper,
	flexRender,
	tableFeatures,
	useTable,
} from "@tanstack/react-table";
import { useMemo } from "react";
import { formatChf } from "./amount";
import { accountsQueryOptions } from "./api";
import { rowTint, useScrollHighlightIntoView } from "./highlight";
import { accountLabel } from "./KontoPicker";
import type { JournalEntry } from "./types";

const features = tableFeatures({});
const helper = createColumnHelper<typeof features, JournalEntry>();

type Props = {
	entries: JournalEntry[];
	selectedId: number | null;
	highlightedIds: number[];
	onSelect: (entry: JournalEntry) => void;
};

/** The Journal: every Entry with the date of its Transaction and the running balance. */
export function JournalList({
	entries,
	selectedId,
	highlightedIds,
	onSelect,
}: Props) {
	const containerRef = useScrollHighlightIntoView(highlightedIds);
	const { data: accounts } = useSuspenseQuery(accountsQueryOptions);

	const columns = useMemo(() => {
		const byNumber = new Map(accounts.map((a) => [a.number, a]));
		return helper.columns([
			helper.accessor("date", {
				header: "Datum",
				cell: (info) => new Date(info.getValue()).toLocaleDateString("de-CH"),
			}),
			helper.accessor("text", { header: "Text" }),
			helper.accessor("accountNumber", {
				header: "Konto",
				cell: (info) => {
					const account = byNumber.get(info.getValue());
					return account ? accountLabel(account) : info.getValue();
				},
			}),
			helper.accessor("amount", {
				header: () => <span className="block text-right">Betrag</span>,
				cell: (info) => (
					<span
						className={`block text-right tabular-nums ${info.getValue() < 0 ? "text-red-700" : ""}`}
					>
						{formatChf(info.getValue())}
					</span>
				),
			}),
			helper.accessor("balance", {
				// Expected Balance
				header: () => <span className="block text-right">Kontostand</span>,
				cell: (info) => (
					<span className="block text-right tabular-nums">
						{formatChf(info.getValue())}
					</span>
				),
			}),
		]);
	}, [accounts]);

	const table = useTable({ features, columns, data: entries });
	const rows = table.getRowModel().rows;

	return (
		<div
			ref={containerRef}
			className="min-w-0 flex-1 overflow-x-auto rounded border border-slate-200"
		>
			<table className="w-full text-left text-sm">
				<thead className="border-b border-slate-200 bg-slate-50">
					{table.getHeaderGroups().map((headerGroup) => (
						<tr key={headerGroup.id}>
							{headerGroup.headers.map((header) => (
								<th key={header.id} className="px-3 py-2 font-medium">
									{header.isPlaceholder
										? null
										: flexRender(
												header.column.columnDef.header,
												header.getContext(),
											)}
								</th>
							))}
						</tr>
					))}
				</thead>
				<tbody>
					{rows.length === 0 ? (
						<tr>
							<td
								colSpan={columns.length}
								className="px-3 py-8 text-center text-slate-500"
							>
								Noch keine Buchungen.
							</td>
						</tr>
					) : (
						rows.map((row) => {
							const entry = row.original;
							const selected = entry.id === selectedId;
							const highlighted = highlightedIds.includes(entry.id);
							return (
								<tr
									key={row.id}
									onClick={() => onSelect(entry)}
									aria-selected={selected}
									data-highlighted={highlighted}
									className={`cursor-pointer border-b border-slate-100 last:border-0 ${rowTint(selected, highlighted)}`}
								>
									{row.getAllCells().map((cell) => (
										<td key={cell.id} className="px-3 py-2">
											{flexRender(
												cell.column.columnDef.cell,
												cell.getContext(),
											)}
										</td>
									))}
								</tr>
							);
						})
					)}
				</tbody>
			</table>
		</div>
	);
}

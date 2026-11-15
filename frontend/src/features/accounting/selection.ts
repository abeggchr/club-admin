/** The selected row: a Transaction, or an Entry (with the Transaction it belongs to). */
export type Selection =
	| { kind: "transaction"; id: number }
	| { kind: "entry"; id: number; transactionId: number };

/** The rows shown in the lighter highlight tint (the selected row itself is not included). */
export type Highlight = { transactionIds: number[]; entryIds: number[] };

/** Which Transactions and Entries are highlighted because of the selection. */
export function highlightOf(
	selection: Selection | null,
	entries: { id: number; transactionId: number }[],
): Highlight {
	if (!selection) return { transactionIds: [], entryIds: [] };
	if (selection.kind === "transaction") {
		return {
			transactionIds: [],
			entryIds: entries
				.filter((e) => e.transactionId === selection.id)
				.map((e) => e.id),
		};
	}
	return {
		transactionIds: [selection.transactionId],
		entryIds: entries
			.filter(
				(e) =>
					e.transactionId === selection.transactionId && e.id !== selection.id,
			)
			.map((e) => e.id),
	};
}

/** The selection after a click: the clicked row, or none when the already selected row is clicked again. */
export function toggleSelection(
	current: Selection | null,
	clicked: Selection,
): Selection | null {
	const same =
		current !== null &&
		current.kind === clicked.kind &&
		current.id === clicked.id;
	return same ? null : clicked;
}

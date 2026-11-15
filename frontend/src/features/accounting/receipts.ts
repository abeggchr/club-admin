/** The list label of a Receipt: its 1-based position by upload order and the date it was added. */
export function receiptLabel(index: number, uploadedAt: string): string {
	const date = new Date(uploadedAt).toLocaleDateString("de-CH", {
		day: "2-digit",
		month: "2-digit",
		year: "numeric",
	});
	return `Beleg ${index + 1} · ${date}`;
}

/** The note in the delete confirmation of a Transaction that has Receipts; undefined when there are none. */
export function deleteWarning(receiptCount: number): string | undefined {
	if (receiptCount === 0) return undefined;
	return receiptCount === 1
		? "1 Beleg wird mit gelöscht."
		: `${receiptCount} Belege werden mit gelöscht.`;
}

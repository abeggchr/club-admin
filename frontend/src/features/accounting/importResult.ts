import type { TransactionImportResult } from "./api";

/**
 * The success lines of the import dialog (ticket #62): the imported/duplicate line plus the two new
 * lines reporting the auto-booked Rechnungsnummern and the movements still without a Buchung.
 */
export function importResultLines(result: TransactionImportResult): string[] {
	const lines = [
		`${result.imported} importiert, ${result.skippedDuplicates} als Duplikat übersprungen.`,
	];
	if (result.bookedInvoices.length > 0) {
		lines.push(
			`Rechnungen ${result.bookedInvoices.join(", ")} automatisch gebucht.`,
		);
	}
	if (result.unmatched > 0) {
		lines.push(
			`${result.unmatched} Bewegung${result.unmatched === 1 ? "" : "en"} ohne Buchung.`,
		);
	}
	return lines;
}

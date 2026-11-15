/** Inline check of an Entry date against the Opening Balance start date (ISO yyyy-mm-dd strings compare lexically). */
export function entryDateError(
	date: string,
	openingStartDate: string | undefined,
): string | undefined {
	if (date === "") return "Datum ist erforderlich.";
	if (openingStartDate && date < openingStartDate) {
		const [y, m, d] = openingStartDate.split("-");
		return `Das Datum liegt vor dem Anfangssaldo (${d}.${m}.${y}).`;
	}
	return undefined;
}

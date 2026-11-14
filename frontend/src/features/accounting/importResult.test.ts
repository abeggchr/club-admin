import { describe, expect, it } from "vitest";
import { importResultLines } from "./importResult";

const base = {
	imported: 3,
	skippedDuplicates: 1,
	bookedInvoices: [] as number[],
	unmatched: 0,
};

describe("importResultLines", () => {
	it("renders the imported/duplicate line", () => {
		expect(importResultLines(base)).toEqual([
			"3 importiert, 1 als Duplikat übersprungen.",
		]);
	});

	it("shows the auto-booked Rechnungsnummern in import order", () => {
		expect(importResultLines({ ...base, bookedInvoices: [42, 43] })).toEqual([
			"3 importiert, 1 als Duplikat übersprungen.",
			"Rechnungen 42, 43 automatisch gebucht.",
		]);
	});

	it("shows the count of movements without a Buchung", () => {
		expect(importResultLines({ ...base, unmatched: 2 })).toEqual([
			"3 importiert, 1 als Duplikat übersprungen.",
			"2 Bewegungen ohne Buchung.",
		]);
	});

	it("shows a singular Bewegung for one unmatched movement", () => {
		expect(importResultLines({ ...base, unmatched: 1 })).toEqual([
			"3 importiert, 1 als Duplikat übersprungen.",
			"1 Bewegung ohne Buchung.",
		]);
	});

	it("shows no extra line when everything was booked", () => {
		expect(importResultLines({ ...base, imported: 2, unmatched: 0 })).toEqual([
			"2 importiert, 1 als Duplikat übersprungen.",
		]);
	});
});

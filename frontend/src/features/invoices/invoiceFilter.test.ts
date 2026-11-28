import { describe, expect, it } from "vitest";
import type { InvoiceListItem } from "./api";
import { invoiceFilters } from "./invoiceFilter";
import type { InvoiceStatus } from "./invoiceStatus";

const invoice = (status: InvoiceStatus): InvoiceListItem => ({
	id: 1,
	number: "0001",
	date: "2026-01-01",
	person: { id: 1, name: "Muster Anna" },
	total: 100,
	status,
});

const keysMatching = (status: InvoiceStatus) =>
	Object.entries(invoiceFilters)
		.filter(([, f]) => f.filter(invoice(status)))
		.map(([key]) => key);

describe("invoiceFilters", () => {
	it("offers Offene, Nicht versendete, Bezahlte, Stornierte and Alle in that order", () => {
		expect(Object.values(invoiceFilters).map((f) => f.label)).toEqual([
			"Offene",
			"Nicht versendete",
			"Bezahlte",
			"Stornierte",
			"Alle",
		]);
	});

	it("shows Created and Sent Rechnungen as Offene", () => {
		expect(keysMatching("Created")).toContain("open");
		expect(keysMatching("Sent")).toContain("open");
		expect(keysMatching("Paid")).not.toContain("open");
		expect(keysMatching("Cancelled")).not.toContain("open");
	});

	it("shows only Created Rechnungen as Nicht versendete", () => {
		expect(keysMatching("Created")).toContain("unsent");
		expect(keysMatching("Sent")).not.toContain("unsent");
	});

	it("shows a Sent Rechnung neither as Bezahlte nor as Stornierte", () => {
		expect(keysMatching("Sent")).toEqual(["open", "all"]);
	});
});

import { describe, expect, it } from "vitest";
import type { InvoiceListItem } from "./api";
import { filterOpenInvoices } from "./InvoicePicker";

const invoice = (
	id: number,
	status: InvoiceListItem["status"],
): InvoiceListItem => ({
	id,
	number: String(id).padStart(4, "0"),
	date: "2026-01-01",
	person: { id: 1, name: "Muster Anna" },
	total: 100,
	status,
});

describe("filterOpenInvoices", () => {
	it("offers Created and Sent Invoices but not Paid or Cancelled ones", () => {
		const invoices = [
			invoice(1, "Created"),
			invoice(2, "Sent"),
			invoice(3, "Paid"),
			invoice(4, "Cancelled"),
		];
		expect(filterOpenInvoices(invoices, "").map((i) => i.id)).toEqual([1, 2]);
	});
});

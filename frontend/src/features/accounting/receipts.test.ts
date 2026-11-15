import { describe, expect, it } from "vitest";
import { deleteWarning, receiptLabel } from "./receipts";

describe("receiptLabel", () => {
	// Midday UTC, so the local date is the same in every timezone.
	const uploadedAt = "2026-11-15T12:00:00Z";

	it("numbers the Receipts from 1 by upload order and shows the upload date", () => {
		expect(receiptLabel(0, uploadedAt)).toBe("Beleg 1 · 15.11.2026");
		expect(receiptLabel(2, uploadedAt)).toBe("Beleg 3 · 15.11.2026");
	});
});

describe("deleteWarning", () => {
	it("is empty for a Transaction without Receipts", () => {
		expect(deleteWarning(0)).toBeUndefined();
	});

	it("names the number of Receipts that go with the Transaction", () => {
		expect(deleteWarning(1)).toBe("1 Beleg wird mit gelöscht.");
		expect(deleteWarning(3)).toBe("3 Belege werden mit gelöscht.");
	});
});

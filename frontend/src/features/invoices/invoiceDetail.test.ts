import { describe, expect, it } from "vitest";
import { statusLabel } from "./api";
import { addressLines, formatDateTime } from "./invoiceDetail";

describe("formatDateTime", () => {
	it("shows date and time of a status step", () => {
		expect(formatDateTime("2026-03-05T14:07:09.123")).toBe("05.03.2026 14:07");
	});
});

describe("statusLabel", () => {
	it("labels a paid Rechnung as Bezahlt", () => {
		expect(statusLabel("Paid")).toBe("Bezahlt");
	});
});

describe("addressLines", () => {
	it("lists street and postal code with city", () => {
		expect(
			addressLines({
				street: "Dorfstrasse",
				number: "1",
				postalCode: "8000",
				city: "Zürich",
				country: null,
			}),
		).toEqual(["Dorfstrasse 1", "8000 Zürich"]);
	});
	it("adds the country only when set", () => {
		expect(
			addressLines({
				street: null,
				number: null,
				postalCode: "8000",
				city: "Zürich",
				country: "Schweiz",
			}),
		).toEqual(["8000 Zürich", "Schweiz"]);
	});
	it("is empty for a person without address", () => {
		expect(
			addressLines({
				street: null,
				number: null,
				postalCode: null,
				city: null,
				country: null,
			}),
		).toEqual([]);
	});
});

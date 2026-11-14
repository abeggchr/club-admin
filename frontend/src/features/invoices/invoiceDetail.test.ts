import { describe, expect, it } from "vitest";
import { statusLabel } from "./api";
import { formatDateTime } from "./invoiceDetail";

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

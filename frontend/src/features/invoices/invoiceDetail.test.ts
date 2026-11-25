import { describe, expect, it } from "vitest";
import { statusLabel } from "./api";
import { formatDateTime, sendBlockedReason } from "./invoiceDetail";

describe("formatDateTime", () => {
	it("shows date and time of a status step", () => {
		expect(formatDateTime("2026-03-05T14:07:09.123")).toBe("05.03.2026 14:07");
	});
});

describe("sendBlockedReason", () => {
	it("allows sending when email is configured and the Recipient has an address", () => {
		expect(sendBlockedReason(true, "anna@example.ch")).toBeNull();
	});

	it("explains that email is not configured", () => {
		expect(sendBlockedReason(false, "anna@example.ch")).toBe(
			"E-Mail ist nicht eingerichtet.",
		);
	});

	it.each([
		null,
		"",
		"  ",
	])("explains that the Recipient has no email address (%j)", (email) => {
		expect(sendBlockedReason(true, email)).toBe(
			"Die Person hat keine E-Mail-Adresse.",
		);
	});
});

describe("statusLabel", () => {
	it("labels a sent Rechnung as Versendet", () => {
		expect(statusLabel("Sent")).toBe("Versendet");
	});

	it("labels a paid Rechnung as Bezahlt", () => {
		expect(statusLabel("Paid")).toBe("Bezahlt");
	});
});

import { describe, expect, it } from "vitest";
import { isOpen, statusLabel } from "./invoiceStatus";

describe("isOpen", () => {
	it("counts a Created and a Sent Invoice as open", () => {
		expect(isOpen("Created")).toBe(true);
		expect(isOpen("Sent")).toBe(true);
	});

	it("does not count a Paid or a Cancelled Invoice as open", () => {
		expect(isOpen("Paid")).toBe(false);
		expect(isOpen("Cancelled")).toBe(false);
	});
});

describe("statusLabel", () => {
	it("labels each Invoice Status with its German term", () => {
		expect(statusLabel("Created")).toBe("Erstellt");
		expect(statusLabel("Sent")).toBe("Versendet");
		expect(statusLabel("Paid")).toBe("Bezahlt");
		expect(statusLabel("Cancelled")).toBe("Storniert");
	});
});

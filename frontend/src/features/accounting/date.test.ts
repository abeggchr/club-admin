import { describe, expect, it } from "vitest";
import { entryDateError } from "./date";

describe("entryDateError", () => {
	it("requires a date", () => {
		expect(entryDateError("", "2026-01-01")).toBe("Datum ist erforderlich.");
	});

	it("rejects a date before the Anfangssaldo", () => {
		expect(entryDateError("2025-12-31", "2026-01-01")).toBe(
			"Das Datum liegt vor dem Anfangssaldo (01.01.2026).",
		);
	});

	it("accepts the start date and later dates", () => {
		expect(entryDateError("2026-01-01", "2026-01-01")).toBeUndefined();
		expect(entryDateError("2026-05-01", "2026-01-01")).toBeUndefined();
	});

	it("accepts any date when there is no Anfangssaldo yet", () => {
		expect(entryDateError("2020-01-01", undefined)).toBeUndefined();
	});
});

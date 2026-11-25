import { describe, expect, it } from "vitest";
import { parseIncomeStatementView } from "./view";

describe("parseIncomeStatementView", () => {
	it("returns simplified when the view param is missing", () => {
		expect(parseIncomeStatementView({})).toBe("simplified");
	});

	it("returns the view for the simplified and all params", () => {
		expect(parseIncomeStatementView({ view: "simplified" })).toBe("simplified");
		expect(parseIncomeStatementView({ view: "all" })).toBe("all");
	});

	it("throws a German error for an unknown value", () => {
		expect(() => parseIncomeStatementView({ view: "none" })).toThrowError(
			"Unbekannte Ansicht 'none'. Bitte 'simplified' oder 'all' verwenden.",
		);
		expect(() => parseIncomeStatementView({ view: "SIMPLIFIED" })).toThrowError(
			"Unbekannte Ansicht 'SIMPLIFIED'. Bitte 'simplified' oder 'all' verwenden.",
		);
	});
});

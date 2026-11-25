import { describe, expect, it } from "vitest";
import { parseIncomeStatementView } from "./view";

describe("parseIncomeStatementView", () => {
	it("returns simplified when the view param is missing", () => {
		expect(parseIncomeStatementView({})).toBe("simplified");
	});

	it("returns simplified for the simplified param", () => {
		expect(parseIncomeStatementView({ view: "simplified" })).toBe("simplified");
	});

	it("returns all for the all param", () => {
		expect(parseIncomeStatementView({ view: "all" })).toBe("all");
	});

	it("falls back to simplified for an unknown value", () => {
		expect(parseIncomeStatementView({ view: "none" })).toBe("simplified");
		expect(parseIncomeStatementView({ view: "SIMPLIFIED" })).toBe("simplified");
	});
});

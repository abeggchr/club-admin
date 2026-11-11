import { describe, expect, it } from "vitest";
import { filterAccounts } from "./kontoFilter";
import type { Account } from "./types";

const accounts: Account[] = [
	{ number: 3400, name: "Jahresbeiträge", category: "Income", active: true },
	{ number: 4200, name: "Hallenmiete", category: "Expense", active: true },
	{ number: 4300, name: "Altes Konto", category: "Expense", active: false },
];

describe("filterAccounts", () => {
	it("returns all active Konten for an empty query", () => {
		expect(filterAccounts(accounts, "", null).map((a) => a.number)).toEqual([
			3400, 4200,
		]);
	});
	it("matches a Kontonummer prefix", () => {
		expect(filterAccounts(accounts, "42", null).map((a) => a.number)).toEqual([
			4200,
		]);
	});
	it("matches name text case-insensitively", () => {
		expect(
			filterAccounts(accounts, "beitr", null).map((a) => a.number),
		).toEqual([3400]);
	});
	it("keeps an inactive current Konto", () => {
		expect(filterAccounts(accounts, "43", 4300).map((a) => a.number)).toEqual([
			4300,
		]);
	});
});

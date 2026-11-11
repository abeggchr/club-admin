import { describe, expect, it } from "vitest";
import { type JubileePerson, sortJubilees } from "./api";

function row(
	years: number,
	lastName: string,
	firstName = "Frida",
): JubileePerson {
	return { personId: 1, years, firstName, lastName };
}

describe("sortJubilees", () => {
	it("sorts by years descending", () => {
		expect(
			sortJubilees([row(10, "A"), row(70, "B"), row(40, "C")]).map(
				(r) => r.years,
			),
		).toEqual([70, 40, 10]);
	});

	it("breaks ties by last name, then first name", () => {
		const rows = [
			row(20, "Meier", "B"),
			row(20, "Muster", "Anna"),
			row(20, "Meier", "A"),
		];
		expect(
			sortJubilees(rows).map((r) => `${r.lastName} ${r.firstName}`),
		).toEqual(["Meier A", "Meier B", "Muster Anna"]);
	});
});

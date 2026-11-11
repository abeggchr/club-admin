import { describe, expect, it } from "vitest";
import { membershipLabels, membershipTypes } from "./membership";

describe("membership types", () => {
	it("lists exactly the six membership types of the club", () => {
		expect(membershipTypes).toEqual([
			"family",
			"single",
			"junior",
			"honorary",
			"free",
			"patron",
		]);
	});

	it("labels each type with its German name", () => {
		expect(
			Object.fromEntries(membershipTypes.map((t) => [t, membershipLabels[t]])),
		).toEqual({
			family: "Familienmitglied",
			single: "Einzelmitglied",
			junior: "Junior:innen",
			honorary: "Ehrenmitglied",
			free: "Freimitglied",
			patron: "Gönner:innen",
		});
	});
});

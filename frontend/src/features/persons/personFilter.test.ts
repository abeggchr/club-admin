import { describe, expect, it } from "vitest";
import {
	canBePayerOf,
	filterPersons,
	payerCandidates,
	personsPaidBy,
} from "./personFilter";
import type { Person } from "./types";

function person(
	id: number,
	firstName: string,
	lastName: string,
	leftOn: string | null = null,
): Person {
	return {
		id,
		firstName,
		lastName,
		leftOn,
		membership: "single" as const,
		dateOfBirth: null,
		joinedOn: null,
		email: null,
		mobilePhone: null,
		landlinePhone: null,
		street: null,
		number: null,
		postalCode: null,
		city: null,
		country: null,
		isMainPayer: false,
		notes: null,
		payerId: null,
	};
}

const persons = [
	person(1, "Anna", "Meier"),
	person(2, "Beat", "Alt", "2025-01-01"),
	person(3, "Carla", "Müller"),
];

describe("filterPersons", () => {
	it("hides persons who have left and sorts by last name", () => {
		expect(filterPersons(persons, "", null).map((p) => p.id)).toEqual([1, 3]);
	});
	it("keeps a left person when they are the current value", () => {
		expect(filterPersons(persons, "", 2).map((p) => p.id)).toEqual([2, 1, 3]);
	});
	it("offers persons who have left when former members are included", () => {
		expect(
			filterPersons(persons, "", null, { includeFormer: true }).map(
				(p) => p.id,
			),
		).toEqual([2, 1, 3]);
	});
	it("matches parts of first and last name in any order", () => {
		expect(filterPersons(persons, "meier an", null).map((p) => p.id)).toEqual([
			1,
		]);
		expect(filterPersons(persons, "mü", null).map((p) => p.id)).toEqual([3]);
	});
});

describe("Zahler", () => {
	const anna = person(1, "Anna", "A");
	const ben = person(2, "Ben", "B");
	const cleo = { ...person(3, "Cleo", "C"), payerId: 2 };
	const all = [anna, ben, cleo];

	it("candidates exclude the Person themselves and Persons who have a Zahler", () => {
		expect(payerCandidates(all, anna).map((p) => p.id)).toEqual([2]);
	});

	it("a Person with their own Zahler cannot be a Zahler", () => {
		expect(canBePayerOf(cleo, anna)).toBe(false);
		expect(canBePayerOf(ben, anna)).toBe(true);
		expect(canBePayerOf(anna, anna)).toBe(false);
	});

	it("lists the Persons a Zahler pays for", () => {
		expect(personsPaidBy(all, ben).map((p) => p.id)).toEqual([3]);
		expect(personsPaidBy(all, anna)).toEqual([]);
	});
});

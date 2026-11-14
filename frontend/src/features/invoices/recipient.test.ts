import { describe, expect, it } from "vitest";
import type { Person } from "../persons/types";
import { pickRecipient } from "./recipient";

function person(id: number, overrides: Partial<Person> = {}): Person {
	return {
		id,
		firstName: `F${id}`,
		lastName: `L${id}`,
		payerId: null,
		isMainPayer: false,
		...overrides,
	} as Person;
}

const persons = [
	person(1, { payerId: 2 }),
	person(2),
	person(3),
	person(4, { isMainPayer: true }),
];

describe("pickRecipient", () => {
	it("switches to the Zahler and names the picked Person for the hint", () => {
		expect(pickRecipient(persons, 1)).toEqual({ recipientId: 2, payerOf: 1 });
	});

	it("keeps a Person without Zahler as recipient, without hint", () => {
		expect(pickRecipient(persons, 3)).toEqual({
			recipientId: 3,
			payerOf: null,
		});
	});

	it("keeps a Hauptzahler as recipient themselves", () => {
		expect(pickRecipient(persons, 4)).toEqual({
			recipientId: 4,
			payerOf: null,
		});
	});

	it("handles clearing the recipient", () => {
		expect(pickRecipient(persons, null)).toEqual({
			recipientId: null,
			payerOf: null,
		});
	});
});

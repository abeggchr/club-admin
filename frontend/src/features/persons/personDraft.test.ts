import { describe, expect, it } from "vitest";
import {
	canBePayerOf,
	canHavePayer,
	choosePayer,
	draftFromPerson,
	isPayerStale,
	isRequired,
	personsPaidBy,
	toPersonPayload,
} from "./personDraft";
import type { Person } from "./types";

function person(id: number, overrides: Partial<Person> = {}): Person {
	return {
		id,
		firstName: `Vorname${id}`,
		lastName: `Nachname${id}`,
		membership: "single",
		dateOfBirth: null,
		joinedOn: null,
		leftOn: null,
		email: null,
		isMainPayer: false,
		receivesClubNews: false,
		mobilePhone: null,
		landlinePhone: null,
		street: null,
		number: null,
		postalCode: null,
		city: null,
		country: null,
		notes: null,
		payerId: null,
		...overrides,
	};
}

// Ben pays for Cleo.
const anna = person(1);
const ben = person(2);
const cleo = person(3, { payerId: 2 });
const all = [anna, ben, cleo];

describe("draft", () => {
	it("shows empty fields as blank text", () => {
		const draft = draftFromPerson(person(1, { email: "a@b.ch" }));
		expect(draft.email).toBe("a@b.ch");
		expect(draft.street).toBe("");
		expect(draft.dateOfBirth).toBe("");
	});

	it("keeps the Payer choice of the Person", () => {
		const draft = draftFromPerson(cleo);
		expect(draft.payerId).toBe(2);
		expect(draft.isMainPayer).toBe(false);
	});

	it("requires first and last name only", () => {
		expect(isRequired("firstName")).toBe(true);
		expect(isRequired("lastName")).toBe(true);
		expect(isRequired("email")).toBe(false);
		expect(isRequired("dateOfBirth")).toBe(false);
	});
});

describe("Payer choice", () => {
	it("choosing a Payer clears Main Payer", () => {
		const draft = choosePayer(
			draftFromPerson(person(1, { isMainPayer: true })),
			2,
		);
		expect(draft.payerId).toBe(2);
		expect(draft.isMainPayer).toBe(false);
	});

	it("choosing Main Payer clears the Payer", () => {
		const draft = choosePayer(draftFromPerson(cleo), "mainPayer");
		expect(draft.payerId).toBeNull();
		expect(draft.isMainPayer).toBe(true);
	});

	it("choosing Regulär clears both the Payer and Main Payer", () => {
		expect(choosePayer(draftFromPerson(cleo), "regular")).toMatchObject({
			payerId: null,
			isMainPayer: false,
		});
		expect(
			choosePayer(draftFromPerson(person(1, { isMainPayer: true })), "regular"),
		).toMatchObject({ payerId: null, isMainPayer: false });
	});
});

describe("payload", () => {
	it("blank fields are sent as null", () => {
		const draft = {
			...draftFromPerson(person(1, { email: "a@b.ch" })),
			email: "",
			city: "Baden",
		};
		const payload = toPersonPayload(draft, all);
		expect(payload.email).toBeNull();
		expect(payload.street).toBeNull();
		expect(payload.city).toBe("Baden");
	});

	it("a stale Payer is sent as null", () => {
		// Anna chose Ben, who has since got Cleo as Payer.
		const persons = [anna, { ...ben, payerId: 3 }, { ...cleo, payerId: null }];
		const draft = choosePayer(draftFromPerson(anna), 2);
		expect(isPayerStale(draft, persons)).toBe(true);
		expect(toPersonPayload(draft, persons).payerId).toBeNull();
	});

	it("a valid Payer is sent as chosen", () => {
		const draft = choosePayer(draftFromPerson(anna), 2);
		expect(isPayerStale(draft, all)).toBe(false);
		expect(toPersonPayload(draft, all).payerId).toBe(2);
	});

	it("sends the Payer choice and the other fields unchanged", () => {
		const draft = choosePayer(draftFromPerson(anna), "mainPayer");
		expect(toPersonPayload(draft, all)).toEqual({
			...anna,
			isMainPayer: true,
		});
	});
});

describe("Payer eligibility", () => {
	it("a Person cannot be their own Payer", () => {
		expect(canBePayerOf(anna, anna)).toBe(false);
	});

	it("a Person with a Payer of their own cannot be a Payer", () => {
		expect(canBePayerOf(cleo, anna)).toBe(false);
		expect(canBePayerOf(ben, anna)).toBe(true);
	});

	it("a Person who is already someone's Payer may be chosen as Payer", () => {
		expect(canBePayerOf(ben, person(4))).toBe(true);
	});

	it("a Main Payer may be chosen as Payer", () => {
		expect(canBePayerOf(person(4, { isMainPayer: true }), anna)).toBe(true);
	});

	it("a Person who is someone's Payer cannot get a Payer or Main Payer", () => {
		expect(canHavePayer(all, ben)).toBe(false);
		expect(canHavePayer(all, anna)).toBe(true);
		expect(canHavePayer(all, cleo)).toBe(true);
	});

	it("lists the Persons a Payer pays for", () => {
		expect(personsPaidBy(all, ben).map((p) => p.id)).toEqual([3]);
		expect(personsPaidBy(all, anna)).toEqual([]);
	});
});

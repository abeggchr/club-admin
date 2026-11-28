import type { Person } from "./types";

const textFields = [
	"firstName",
	"lastName",
	"dateOfBirth",
	"joinedOn",
	"leftOn",
	"mobilePhone",
	"landlinePhone",
	"email",
	"street",
	"number",
	"postalCode",
	"city",
	"country",
	"notes",
] as const;

/** A field the Person form edits as text; an empty one is sent as null. */
export type PersonTextField = (typeof textFields)[number];

/** The Person form's values as the user sees them: every text field is a string, "" when empty. */
export type PersonDraft = Omit<Person, PersonTextField> &
	Record<PersonTextField, string>;

/** The Payer picker's choice: "Regulär", Main Payer, or the id of a named Payer. */
export type PayerChoice = "regular" | "mainPayer" | number;

export function draftFromPerson(person: Person): PersonDraft {
	const draft = { ...person } as PersonDraft;
	for (const field of textFields) draft[field] = person[field] ?? "";
	return draft;
}

/** Whether the field must be filled in before saving. */
export function isRequired(field: PersonTextField): boolean {
	return field === "firstName" || field === "lastName";
}

/** A named Payer clears Main Payer, and Main Payer or "Regulär" clears the named Payer. */
export function choosePayer(
	draft: PersonDraft,
	choice: PayerChoice,
): PersonDraft {
	return typeof choice === "number"
		? { ...draft, payerId: choice, isMainPayer: false }
		: { ...draft, payerId: null, isMainPayer: choice === "mainPayer" };
}

/** Whether the chosen Payer has since gained a Payer of their own, so is no longer valid and is dropped on save. */
export function isPayerStale(draft: PersonDraft, persons: Person[]): boolean {
	const payer = persons.find((p) => p.id === draft.payerId);
	return payer !== undefined && !canBePayerOf(payer, draft);
}

/** The request body for the draft: empty fields become null (matching the importer), a stale Payer is dropped. */
export function toPersonPayload(draft: PersonDraft, persons: Person[]): Person {
	const payload: Record<string, unknown> = { ...draft };
	for (const field of textFields) {
		if (draft[field] === "") payload[field] = null;
	}
	if (isPayerStale(draft, persons)) payload.payerId = null;
	return payload as Person;
}

/** Whether `candidate` may be the Payer of `person`: not the Person themselves, and not someone who already has a Payer. */
export function canBePayerOf(
	candidate: Person,
	person: Pick<Person, "id">,
): boolean {
	return candidate.id !== person.id && candidate.payerId === null;
}

/** Whether `person` may get a Payer or Main Payer: a Person who is someone's Payer may not, so their picker is locked. */
export function canHavePayer(persons: Person[], person: Person): boolean {
	return personsPaidBy(persons, person).length === 0;
}

/** The Persons for whom `payer` is the Payer. */
export function personsPaidBy(persons: Person[], payer: Person): Person[] {
	return persons.filter((p) => p.payerId === payer.id);
}

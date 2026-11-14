import type { Person } from "./types";

export function personLabel(person: Person): string {
	return `${person.lastName} ${person.firstName}`;
}

/**
 * Persons matching what the user typed (any part of first or last name, case-insensitive), sorted by last then
 * first name. Persons who have left (leftOn set) are only offered when they are the current value.
 */
export function filterPersons(
	persons: Person[],
	query: string,
	currentValue: number | null,
	options: { includeFormer?: boolean } = {},
): Person[] {
	const words = query.trim().toLowerCase().split(/\s+/).filter(Boolean);
	return persons
		.filter(
			(p) =>
				options.includeFormer || p.leftOn === null || p.id === currentValue,
		)
		.filter((p) => {
			const name = `${p.firstName} ${p.lastName}`.toLowerCase();
			return words.every((w) => name.includes(w));
		})
		.sort(
			(a, b) =>
				a.lastName.localeCompare(b.lastName, "de") ||
				a.firstName.localeCompare(b.firstName, "de"),
		);
}

/** Whether `candidate` may be the Zahler of `person`: not the Person themselves, and not someone who already has a Zahler. */
export function canBePayerOf(candidate: Person, person: Person): boolean {
	return candidate.id !== person.id && candidate.payerId === null;
}

/** Persons who can be the Zahler of `person`. */
export function payerCandidates(persons: Person[], person: Person): Person[] {
	return persons.filter((p) => canBePayerOf(p, person));
}

/** Whether `person` may have a Zahler: a Person who is someone's Zahler may not. */
export function canHavePayer(persons: Person[], person: Person): boolean {
	return personsPaidBy(persons, person).length === 0;
}

/** The Persons for whom `payer` is the Zahler. */
export function personsPaidBy(persons: Person[], payer: Person): Person[] {
	return persons.filter((p) => p.payerId === payer.id);
}

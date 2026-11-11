import { queryOptions } from "@tanstack/react-query";

export type JubileePerson = {
	personId: number;
	years: number;
	firstName: string;
	lastName: string;
	birthdate?: string;
	entryDate?: string;
};

export type Jubilees = {
	clubYearStartYear: number;
	anniversaries: JubileePerson[];
	jubilees: JubileePerson[];
};

/** One table of the Jubiliäen view, sorted years descending, then last name, then first name. */
export function sortJubilees(rows: JubileePerson[]): JubileePerson[] {
	return [...rows].sort(
		(a, b) =>
			b.years - a.years ||
			a.lastName.localeCompare(b.lastName, "de") ||
			a.firstName.localeCompare(b.firstName, "de"),
	);
}

async function fetchJubilees(): Promise<Jubilees> {
	const res = await fetch("/api/jubilees");
	if (!res.ok) throw new Error(`Backend returned ${res.status}`);
	return res.json();
}

export const jubileesQueryOptions = queryOptions({
	queryKey: ["jubilees"],
	queryFn: fetchJubilees,
});

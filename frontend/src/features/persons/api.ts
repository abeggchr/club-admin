import { queryOptions } from "@tanstack/react-query";
import type { Person } from "./types";

async function fetchPersons(): Promise<Person[]> {
	const res = await fetch("/api/persons");
	if (!res.ok) throw new Error(`Backend returned ${res.status}`);
	return res.json();
}

export const personsQueryOptions = queryOptions({
	queryKey: ["persons"],
	queryFn: fetchPersons,
});

export async function importPersons(file: File): Promise<number> {
	const body = new FormData();
	body.append("file", file);
	const res = await fetch("/api/persons/import", { method: "POST", body });
	if (!res.ok) {
		const data = await res.json().catch(() => null);
		throw new Error(data?.error ?? `Backend returned ${res.status}`);
	}
	return (await res.json()).imported;
}

export async function updatePerson(person: Person): Promise<Person> {
	const res = await fetch(`/api/persons/${person.id}`, {
		method: "PUT",
		headers: { "Content-Type": "application/json" },
		body: JSON.stringify(person),
	});
	if (!res.ok) {
		const data = await res.json().catch(() => null);
		throw new Error(data?.error ?? `Backend returned ${res.status}`);
	}
	return res.json();
}

export async function downloadMailingList() {
	const res = await fetch("/api/persons/mailing-list");
	if (!res.ok) throw new Error(`Backend returned ${res.status}`);
	const blob = await res.blob();
	const url = URL.createObjectURL(blob);
	const a = Object.assign(document.createElement("a"), {
		href: url,
		download: "versandliste.csv",
	});
	a.click();
	URL.revokeObjectURL(url);
}

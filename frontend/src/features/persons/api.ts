import { queryOptions } from "@tanstack/react-query";
import { apiFetch } from "../../lib/apiFetch";
import type { Person } from "./types";

/** Throws the backend's German error message, or a generic one, for a non-OK response. */
export async function ensureOk(res: Response): Promise<void> {
	if (res.ok) return;
	const data = (await res.json().catch(() => null)) as {
		error?: string;
	} | null;
	throw new Error(data?.error ?? `Backend returned ${res.status}`);
}

export async function getJson<T>(url: string): Promise<T> {
	const res = await apiFetch(url);
	await ensureOk(res);
	return res.json();
}

export async function requestJson<T>(
	url: string,
	method: "POST" | "PUT",
	body: unknown,
): Promise<T> {
	const res = await apiFetch(url, {
		method,
		headers: { "Content-Type": "application/json" },
		body: JSON.stringify(body),
	});
	await ensureOk(res);
	return res.json();
}

async function fetchPersons(): Promise<Person[]> {
	return getJson<Person[]>("/api/person");
}

export const personsQueryOptions = queryOptions({
	queryKey: ["persons"],
	queryFn: fetchPersons,
});

export async function importPersons(file: File): Promise<number> {
	const body = new FormData();
	body.append("file", file);
	const res = await apiFetch("/api/person/import", { method: "POST", body });
	await ensureOk(res);
	return (await res.json()).imported;
}

export async function updatePerson(person: Person): Promise<Person> {
	return requestJson<Person>(`/api/person/${person.id}`, "PUT", person);
}

/** Deletes a Person; the backend rejects with a German error when the Person is someone's Zahler. */
export async function deletePerson(id: number): Promise<void> {
	await ensureOk(await apiFetch(`/api/person/${id}`, { method: "DELETE" }));
}

export async function downloadMailingList() {
	const res = await apiFetch("/api/person/mailing-list");
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

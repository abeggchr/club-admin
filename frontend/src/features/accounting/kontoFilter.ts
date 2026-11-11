import type { Account } from "./types";

/**
 * Konten matching what the user typed: a Kontonummer prefix or any word-part
 * of the name, case-insensitive. Empty query matches everything. Inactive Konten
 * are only offered when they are the current value.
 */
export function filterAccounts(
	accounts: Account[],
	query: string,
	currentValue: number | null,
): Account[] {
	const q = query.trim().toLowerCase();
	return accounts
		.filter((a) => a.active || a.number === currentValue)
		.filter(
			(a) =>
				q === "" ||
				String(a.number).startsWith(q) ||
				a.name.toLowerCase().includes(q),
		)
		.sort((a, b) => a.number - b.number);
}

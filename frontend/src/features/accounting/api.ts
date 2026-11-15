import { type QueryClient, queryOptions } from "@tanstack/react-query";
import { apiFetch } from "../../lib/apiFetch";
import type {
	Account,
	JournalEntry,
	Transaction,
	TransactionInput,
} from "./types";

export type Settings = {
	openingBalance: number;
	startDate: string;
	iban: string;
	name: string;
	address: string;
	postalCode: string;
	city: string;
};

/** Throws the backend's German error message, or a generic one, for a non-OK response. */
export async function ensureOk(res: Response): Promise<void> {
	if (res.ok) return;
	const data = await res.json().catch(() => null);
	throw new Error(data?.error ?? `Backend returned ${res.status}`);
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

export async function getJson<T>(url: string): Promise<T> {
	const res = await apiFetch(url);
	await ensureOk(res);
	return res.json();
}

export const accountsQueryOptions = queryOptions({
	queryKey: ["accounts"],
	queryFn: () => getJson<Account[]>("/api/accounts"),
	staleTime: Infinity, // the Chart of Accounts only changes with a deploy
});

async function fetchSettings(): Promise<Settings | null> {
	const res = await apiFetch("/api/settings");
	if (res.status === 404) return null;
	await ensureOk(res);
	return res.json();
}

export const settingsQueryOptions = queryOptions({
	queryKey: ["settings"],
	queryFn: fetchSettings,
});

export function saveSettings(settings: Settings): Promise<Settings> {
	return requestJson("/api/settings", "PUT", settings);
}

export type JournalFilters = {
	from: string;
	to: string;
	accountNumber: string;
	search: string;
};

export const noFilters: JournalFilters = {
	from: "",
	to: "",
	accountNumber: "",
	search: "",
};

/** Filters only hide rows; the backend keeps the running balance of the whole journal. */
function fetchEntries(filters: JournalFilters): Promise<JournalEntry[]> {
	const params = new URLSearchParams();
	if (filters.from) params.set("from", filters.from);
	if (filters.to) params.set("to", filters.to);
	if (filters.accountNumber) params.set("account", filters.accountNumber);
	if (filters.search.trim()) params.set("q", filters.search.trim());
	return getJson(`/api/entries?${params}`);
}

export const entriesQueryOptions = queryOptions({
	queryKey: ["entries"],
	queryFn: () => fetchEntries(noFilters),
});

export const transactionsQueryOptions = queryOptions({
	queryKey: ["transactions"],
	queryFn: () => getJson<Transaction[]>("/api/transactions"),
});

/** Creates a Transaction (id null) or replaces an existing one, with all its Entries in one request. */
export function saveTransaction(
	id: number | null,
	input: TransactionInput,
): Promise<Transaction> {
	return requestJson(
		id === null ? "/api/transactions" : `/api/transactions/${id}`,
		id === null ? "POST" : "PUT",
		input,
	);
}

/** Deletes a Transaction together with any Entries still stored for it. */
export async function deleteTransaction(id: number): Promise<void> {
	await ensureOk(
		await apiFetch(`/api/transactions/${id}`, { method: "DELETE" }),
	);
}

export type TransactionImportResult = {
	imported: number;
	skippedDuplicates: number;
	/** The Invoice Numbers of the movements that were auto-booked, in import order. */
	bookedInvoices: number[];
	/** The count of imported movements that came in without Entries. */
	unmatched: number;
};

export type TransactionImportError = {
	file: string | null;
	message: string;
};

/** Thrown when the backend rejects the eBanking zip or XML file; the errors name their file. */
export class TransactionImportFailed extends Error {
	constructor(public readonly errors: TransactionImportError[]) {
		super(
			errors
				.map((e) => (e.file ? `${e.file}: ${e.message}` : e.message))
				.join("\n"),
		);
	}
}

/** Uploads the eBanking zip or a single CAMT.053 XML file as-is; the backend creates one Transaction per simple CAMT entry. */
export async function importTransactions(
	file: File,
): Promise<TransactionImportResult> {
	const body = new FormData();
	body.append("file", file);
	const res = await apiFetch("/api/transactions/import", {
		method: "POST",
		body,
	});
	if (res.ok) return res.json();
	const data = await res.json().catch(() => null);
	if (Array.isArray(data?.errors))
		throw new TransactionImportFailed(data.errors);
	throw new Error(data?.error ?? `Backend returned ${res.status}`);
}

/** Refreshes everything derived from the journal after a Transaction or the settings changed. */
export async function invalidateJournalQueries(
	queryClient: QueryClient,
): Promise<void> {
	await queryClient.invalidateQueries({
		queryKey: entriesQueryOptions.queryKey,
	});
	await queryClient.invalidateQueries({
		queryKey: transactionsQueryOptions.queryKey,
	});
	await queryClient.invalidateQueries({ queryKey: ["expected-balance"] });
}

export type ExpectedBalance = {
	date: string;
	/** Expected Balance from the Entries as of the date. */
	balance: number | null;
	/** running balance from the Transactions (the bank's view) as of the date. */
	transactionBalance: number | null;
	/** True when the two views disagree; "Kontostand heute" turns red. */
	differs: boolean;
};

export const expectedBalanceQueryOptions = (date: string) =>
	queryOptions({
		queryKey: ["expected-balance", date],
		queryFn: () =>
			getJson<ExpectedBalance>(
				`/api/expected-balance?date=${encodeURIComponent(date)}`,
			),
	});

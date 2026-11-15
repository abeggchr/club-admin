import { type QueryClient, queryOptions } from "@tanstack/react-query";
import { apiFetch } from "../../lib/apiFetch";
import { ensureOk, getJson, requestJson } from "../accounting/api";
import type { InvoiceInput } from "./invoiceDraft";

export type InvoiceListItem = {
	id: number;
	number: string;
	/** ISO yyyy-mm-dd */
	date: string;
	person: { id: number; name: string };
	total: number;
	status: string;
};

export const invoicesQueryOptions = queryOptions({
	queryKey: ["invoices"],
	queryFn: () => getJson<InvoiceListItem[]>("/api/invoices"),
});

export type InvoiceDetail = {
	id: number;
	number: string;
	date: string;
	person: {
		id: number;
		name: string;
		street: string | null;
		number: string | null;
		postalCode: string | null;
		city: string | null;
		country: string | null;
	};
	lines: { text: string; amount: number; accountNumber: number }[];
	total: number;
	status: string;
	/** Swiss QR-bill Creditor Reference (RF8008); "" for Invoices created before it was stored. */
	reference: string;
	/** The id of the Transaction that first covered the total, or null. */
	paidBy: number | null;
	statusHistory: { status: string; timestamp: string }[];
};

export const invoiceDetailQueryOptions = (id: number) =>
	queryOptions({
		queryKey: ["invoices", id],
		queryFn: () => getJson<InvoiceDetail>(`/api/invoices/${id}`),
	});

export function invoicePdfUrl(id: number): string {
	return `/api/invoices/${id}/pdf`;
}

/** Creates the Invoice (the backend renders and stores the PDF); rejects with the backend's message. */
export function createInvoice(input: InvoiceInput): Promise<unknown> {
	return requestJson("/api/invoices", "POST", input);
}

export type AnnualFeesSummary = {
	created: number;
	failed: string[];
	warnings: string[];
};

/** Prefixed with the discounts key so that changing a Discount refreshes it. */
export const annualFeesPreviewQueryOptions = queryOptions({
	queryKey: ["membership-discounts", "annual-fees-preview"],
	queryFn: () =>
		getJson<{
			consideredPersons: number;
			plannedInvoices: number;
		}>("/api/invoices/annual-fees/preview"),
});

/** Creates the Annual Fee Invoices for all active members (no preview, no duplicate check). */
export function createAnnualFeeInvoices(
	templateId: number,
): Promise<AnnualFeesSummary> {
	return requestJson("/api/invoices/annual-fees", "POST", { templateId });
}

/** Discount: a Person's percentage discount on the Annual Fee. */
export type MembershipDiscount = {
	personId: number;
	percent: number;
	text: string;
};

export const membershipDiscountsQueryOptions = queryOptions({
	queryKey: ["membership-discounts"],
	queryFn: () =>
		getJson<MembershipDiscount[]>("/api/invoices/membership-discounts"),
});

/** Adds or replaces the Discount of a Person; rejects with the backend's message. */
export function saveMembershipDiscount(
	discount: MembershipDiscount,
): Promise<MembershipDiscount> {
	return requestJson("/api/invoices/membership-discounts", "PUT", discount);
}

export async function deleteMembershipDiscount(
	personId: number,
): Promise<void> {
	await ensureOk(
		await apiFetch(`/api/invoices/membership-discounts/${personId}`, {
			method: "DELETE",
		}),
	);
}

/** Cancels (storniert) an open Invoice; rejects with the backend's message. */
export function cancelInvoice(id: number): Promise<InvoiceDetail> {
	return requestJson(`/api/invoices/${id}/cancel`, "POST", {});
}

export function invalidateInvoices(queryClient: QueryClient) {
	return queryClient.invalidateQueries({
		queryKey: invoicesQueryOptions.queryKey,
	});
}

const statusLabels: Record<string, string> = {
	Created: "Erstellt",
	Paid: "Bezahlt",
	Cancelled: "Storniert",
};

export function statusLabel(status: string): string {
	return statusLabels[status] ?? status;
}

/** yyyy-mm-dd to dd.MM.yyyy */
export function formatDate(iso: string): string {
	const [y, m, d] = iso.split("-");
	return `${d}.${m}.${y}`;
}

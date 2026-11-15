import { queryOptions } from "@tanstack/react-query";
import {
	type KnownMembershipType,
	membershipLabels,
	membershipTypes,
} from "../persons/membership";
import { type PieSlice, pieSlices } from "./pie";

export type PersonsSummary = {
	total: number;
	byMembership: { membership: string; count: number }[];
};

export type InvoicesSummary = {
	openCount: number;
	openAmount: number;
};

async function fetchJson<T>(url: string): Promise<T> {
	const res = await fetch(url);
	if (!res.ok) throw new Error(`Backend returned ${res.status}`);
	return res.json();
}

export const personsSummaryQueryOptions = queryOptions({
	queryKey: ["dashboard", "persons-summary"],
	queryFn: () => fetchJson<PersonsSummary>("/api/person/summary"),
});

export const invoicesSummaryQueryOptions = queryOptions({
	queryKey: ["dashboard", "invoices-summary"],
	queryFn: () => fetchJson<InvoicesSummary>("/api/invoices/summary"),
});

/** The fixed color of each Membership, pie slice and legend alike. */
export const membershipColors: Record<KnownMembershipType, string> = {
	family: "#0369a1",
	single: "#0ea5e9",
	junior: "#10b981",
	honorary: "#8b5cf6",
	free: "#f59e0b",
	patron: "#ef4444",
};

export type MembershipPieSlice = PieSlice & {
	membership: KnownMembershipType;
	label: string;
	count: number;
	color: string;
};

/**
 * The active Persons per Membership as pie slices: ordered by count descending,
 * ties in enum order (the backend already delivers this order), 0 counts left out,
 * each slice in the fixed color of its Membership. Unknown counts toward the total
 * but gets no slice (no fixed color).
 */
export function membershipPie(summary: PersonsSummary): MembershipPieSlice[] {
	const known = summary.byMembership.filter(
		(m) =>
			m.count > 0 && membershipTypes.includes(m.membership as KnownMembershipType),
	);
	const geometry = pieSlices(known.map((m) => m.count));
	return known.map((m, i) => {
		const membership = m.membership as KnownMembershipType;
		return {
			...geometry[i],
			membership,
			label: membershipLabels[membership],
			count: m.count,
			color: membershipColors[membership],
		};
	});
}

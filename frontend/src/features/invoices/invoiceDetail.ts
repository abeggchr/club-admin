import { formatDate } from "./api";

type Address = {
	street: string | null;
	number: string | null;
	postalCode: string | null;
	city: string | null;
	country: string | null;
};

/** "2026-03-05T14:07:09" to "05.03.2026 14:07" */
export function formatDateTime(iso: string): string {
	const [date, time = ""] = iso.split("T");
	return `${formatDate(date)} ${time.slice(0, 5)}`;
}

export function addressLines(a: Address): string[] {
	const place = [a.postalCode, a.city].filter(Boolean).join(" ");
	const address = [a.street, a.number].filter(Boolean).join(" ");
	return [address, place, a.country].filter((l): l is string => !!l);
}

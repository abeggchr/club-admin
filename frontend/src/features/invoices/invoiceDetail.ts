import { formatDate } from "./api";

/** "2026-03-05T14:07:09" to "05.03.2026 14:07" */
export function formatDateTime(iso: string): string {
	const [date, time = ""] = iso.split("T");
	return `${formatDate(date)} ${time.slice(0, 5)}`;
}

export type DeliveryChannel = "Email" | "Post";

export type ChannelOption = {
	channel: DeliveryChannel;
	label: string;
	/** Why the channel cannot be chosen, or null when it can. */
	blockedReason: string | null;
};

type Recipient = {
	email: string | null;
	street: string | null;
	postalCode: string | null;
	city: string | null;
	country: string | null;
};

const swissCountries = ["", "ch", "schweiz"];

/** Email and Post with the reason each one is unavailable (mirrors the backend senders). */
export function deliveryChannels(
	emailConfigured: boolean,
	ePostConfigured: boolean,
	recipient: Recipient,
): ChannelOption[] {
	const swissAddress =
		!!recipient.street?.trim() &&
		!!recipient.postalCode?.trim() &&
		!!recipient.city?.trim() &&
		swissCountries.includes((recipient.country ?? "").trim().toLowerCase());
	return [
		{
			channel: "Email",
			label: "E-Mail",
			blockedReason: !emailConfigured
				? "E-Mail ist nicht eingerichtet: Es braucht eine Absenderadresse und entweder SMTP oder ePost."
				: !recipient.email?.trim()
					? "Die Person hat keine E-Mail-Adresse."
					: null,
		},
		{
			channel: "Post",
			label: "Post",
			blockedReason: !ePostConfigured
				? "ePost ist nicht eingerichtet."
				: !swissAddress
					? "Die Person hat keine vollständige Schweizer Adresse (Strasse, PLZ und Ort)."
					: null,
		},
	];
}

/** Email when usable, else Post, else nothing. */
export function preselectedChannel(
	options: ChannelOption[],
): DeliveryChannel | null {
	return options.find((o) => o.blockedReason === null)?.channel ?? null;
}

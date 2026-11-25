import { formatDate } from "./api";

/** "2026-03-05T14:07:09" to "05.03.2026 14:07" */
export function formatDateTime(iso: string): string {
	const [date, time = ""] = iso.split("T");
	return `${formatDate(date)} ${time.slice(0, 5)}`;
}

/** Why "Versenden" is disabled, or null when sending by email is possible. */
export function sendBlockedReason(
	emailConfigured: boolean,
	recipientEmail: string | null,
): string | null {
	if (!emailConfigured) return "E-Mail ist nicht eingerichtet.";
	if (!recipientEmail?.trim()) return "Die Person hat keine E-Mail-Adresse.";
	return null;
}

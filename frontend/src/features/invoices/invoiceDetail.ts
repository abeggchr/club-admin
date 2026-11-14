import { formatDate } from "./api";

/** "2026-03-05T14:07:09" to "05.03.2026 14:07" */
export function formatDateTime(iso: string): string {
	const [date, time = ""] = iso.split("T");
	return `${formatDate(date)} ${time.slice(0, 5)}`;
}

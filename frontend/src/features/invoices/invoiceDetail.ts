import { formatDate } from "./api";

/** "2026-03-05T14:07:09" to "05.03.2026 14:07" */
export function formatDateTime(iso: string): string {
	const [date, time = ""] = iso.split("T");
	return `${formatDate(date)} ${time.slice(0, 5)}`;
}

export type DeliveryChannel = "Email" | "Post";

/** A Delivery Channel of the Recipient as the backend resolved it. */
export type ChannelOption = {
	channel: DeliveryChannel;
	/** Why the channel cannot be chosen, or null when it can. */
	blockedReason: string | null;
};

const channelLabels: Record<DeliveryChannel, string> = {
	Email: "E-Mail",
	Post: "Post",
};

export function channelLabel(channel: DeliveryChannel): string {
	return channelLabels[channel];
}

/** Email when usable, else Post, else nothing. */
export function preselectedChannel(
	options: ChannelOption[],
): DeliveryChannel | null {
	return options.find((o) => o.blockedReason === null)?.channel ?? null;
}

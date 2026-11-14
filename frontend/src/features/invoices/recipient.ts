import type { Person } from "../persons/types";

export type RecipientPick = {
	recipientId: number | null;
	/** The Person the Zahler was substituted for (drives the hint "Zahler von X"), or null. */
	payerOf: number | null;
};

/**
 * Picking Person X as recipient switches to X's Zahler. A Hauptzahler has no Zahler of their own
 * (payerId is null), so they stay the recipient.
 */
export function pickRecipient(
	persons: Person[],
	pickedId: number | null,
): RecipientPick {
	const picked = persons.find((p) => p.id === pickedId);
	if (!picked || picked.payerId === null)
		return { recipientId: pickedId, payerOf: null };
	return { recipientId: picked.payerId, payerOf: picked.id };
}

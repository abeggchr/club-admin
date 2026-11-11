// The membership types ("Mitgliedschaft") in their display order.
export const membershipTypes = [
	"family",
	"single",
	"junior",
	"honorary",
	"free",
	"patron",
] as const;

export type MembershipType = (typeof membershipTypes)[number];

// The German label of each membership type, as shown in the UI.
export const membershipLabels: Record<MembershipType, string> = {
	family: "Familienmitglied",
	single: "Einzelmitglied",
	junior: "Junior:innen",
	honorary: "Ehrenmitglied",
	free: "Freimitglied",
	patron: "Gönner:innen",
};

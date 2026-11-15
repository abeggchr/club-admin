// The membership types ("Mitgliedschaft") in their display order.
export const membershipTypes = [
	"family",
	"single",
	"junior",
	"honorary",
	"free",
	"patron",
] as const;

// "unknown" is a Person whose membership is not recorded (e.g. an empty CSV value); it is not selectable.
export type KnownMembershipType = (typeof membershipTypes)[number];
export type MembershipType = KnownMembershipType | "unknown";

// The German label of each membership type, as shown in the UI.
export const membershipLabels: Record<MembershipType, string> = {
	family: "Familienmitglied",
	single: "Einzelmitglied",
	junior: "Junior:innen",
	honorary: "Ehrenmitglied",
	free: "Freimitglied",
	patron: "Gönner:innen",
	unknown: "Unbekannt",
};

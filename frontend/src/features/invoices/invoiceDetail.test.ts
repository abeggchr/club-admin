import { describe, expect, it } from "vitest";
import { statusLabel } from "./api";
import {
	deliveryChannels,
	formatDateTime,
	preselectedChannel,
} from "./invoiceDetail";

describe("formatDateTime", () => {
	it("shows date and time of a status step", () => {
		expect(formatDateTime("2026-03-05T14:07:09.123")).toBe("05.03.2026 14:07");
	});
});

const swiss = {
	email: "anna@example.ch",
	street: "Bahnhofstrasse",
	postalCode: "5400",
	city: "Baden",
	country: null,
};

describe("deliveryChannels", () => {
	it("offers both channels when both are configured and the Recipient qualifies", () => {
		const [email, post] = deliveryChannels(true, true, swiss);
		expect(email).toMatchObject({ channel: "Email", blockedReason: null });
		expect(post).toMatchObject({ channel: "Post", blockedReason: null });
	});

	it("explains that email is not configured", () => {
		expect(deliveryChannels(false, true, swiss)[0].blockedReason).toBe(
			"E-Mail ist nicht eingerichtet: Es braucht eine Absenderadresse und entweder SMTP oder ePost.",
		);
	});

	it.each([
		null,
		"",
		"  ",
	])("explains that the Recipient has no email address (%j)", (email) => {
		expect(
			deliveryChannels(true, true, { ...swiss, email })[0].blockedReason,
		).toBe("Die Person hat keine E-Mail-Adresse.");
	});

	it("explains that ePost is not configured", () => {
		expect(deliveryChannels(true, false, swiss)[1].blockedReason).toBe(
			"ePost ist nicht eingerichtet.",
		);
	});

	it.each([
		{ street: null },
		{ postalCode: " " },
		{ city: null },
		{ country: "Deutschland" },
	])("blocks Post for an incomplete or foreign address (%j)", (change) => {
		expect(
			deliveryChannels(true, true, { ...swiss, ...change })[1].blockedReason,
		).toBe(
			"Die Person hat keine vollständige Schweizer Adresse (Strasse, PLZ und Ort).",
		);
	});

	it.each([
		"CH",
		"ch",
		"Schweiz",
		"SCHWEIZ",
		"",
	])("accepts Switzerland as country (%j)", (country) => {
		expect(
			deliveryChannels(true, true, { ...swiss, country })[1].blockedReason,
		).toBeNull();
	});
});

describe("preselectedChannel", () => {
	it("prefers Email when both can be used", () => {
		expect(preselectedChannel(deliveryChannels(true, true, swiss))).toBe(
			"Email",
		);
	});

	it("falls back to Post when email cannot be used", () => {
		expect(
			preselectedChannel(
				deliveryChannels(true, true, { ...swiss, email: null }),
			),
		).toBe("Post");
	});

	it("is null when no channel can be used", () => {
		expect(
			preselectedChannel(deliveryChannels(false, false, swiss)),
		).toBeNull();
	});
});

describe("statusLabel", () => {
	it("labels a sent Rechnung as Versendet", () => {
		expect(statusLabel("Sent")).toBe("Versendet");
	});

	it("labels a paid Rechnung as Bezahlt", () => {
		expect(statusLabel("Paid")).toBe("Bezahlt");
	});
});

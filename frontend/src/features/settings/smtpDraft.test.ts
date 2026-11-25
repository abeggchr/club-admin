import { describe, expect, it } from "vitest";
import { emptySmtpDraft, smtpDraftFrom, smtpPayload } from "./smtpDraft";

describe("smtpPayload", () => {
	it("turns an untouched form into an empty group", () => {
		const result = smtpPayload(emptySmtpDraft);
		expect(result).toEqual({
			ok: true,
			value: {
				host: "",
				port: null,
				security: null,
				username: "",
				password: "",
				fromAddress: "",
				fromName: "",
			},
		});
	});

	it("parses the port into a number and trims the text fields", () => {
		const result = smtpPayload({
			...emptySmtpDraft,
			host: " smtp.example.ch ",
			port: "587",
			security: "StartTls",
		});
		expect(result.ok && result.value.host).toBe("smtp.example.ch");
		expect(result.ok && result.value.port).toBe(587);
		expect(result.ok && result.value.security).toBe("StartTls");
	});

	it("rejects a port that is not a whole number", () => {
		expect(smtpPayload({ ...emptySmtpDraft, port: "58x" })).toEqual({
			ok: false,
			error: "Der Port muss eine ganze Zahl sein.",
		});
	});

	it("keeps the password as typed, without trimming", () => {
		const result = smtpPayload({ ...emptySmtpDraft, password: " a b " });
		expect(result.ok && result.value.password).toBe(" a b ");
	});
});

describe("smtpDraftFrom", () => {
	it("starts with an empty password and shows the stored one as set", () => {
		const draft = smtpDraftFrom({
			host: "smtp.example.ch",
			port: 465,
			security: "Ssl",
			username: "kasse",
			passwordSet: true,
			fromAddress: "kasse@example.ch",
			fromName: null,
		});
		expect(draft).toEqual({
			host: "smtp.example.ch",
			port: "465",
			security: "Ssl",
			username: "kasse",
			password: "",
			fromAddress: "kasse@example.ch",
			fromName: "",
		});
	});
});

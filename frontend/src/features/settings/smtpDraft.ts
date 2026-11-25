export type SmtpSecurity = "StartTls" | "Ssl" | "None";

/** The SMTP group as the backend returns it: never the password, only whether it is set. */
export type SmtpSettings = {
	host: string | null;
	port: number | null;
	security: SmtpSecurity | null;
	username: string | null;
	passwordSet: boolean;
	fromAddress: string | null;
};

/** The SMTP group as the backend accepts it. An empty password keeps the stored one. */
export type SmtpPayload = {
	host: string;
	port: number | null;
	security: SmtpSecurity | null;
	username: string;
	password: string;
	fromAddress: string;
};

export type SmtpDraft = {
	host: string;
	port: string;
	security: SmtpSecurity | "";
	username: string;
	password: string;
	fromAddress: string;
};

export const emptySmtpDraft: SmtpDraft = {
	host: "",
	port: "",
	security: "",
	username: "",
	password: "",
	fromAddress: "",
};

export function smtpDraftFrom(smtp: SmtpSettings): SmtpDraft {
	return {
		host: smtp.host ?? "",
		port: smtp.port === null ? "" : String(smtp.port),
		security: smtp.security ?? "",
		username: smtp.username ?? "",
		password: "",
		fromAddress: smtp.fromAddress ?? "",
	};
}

/** Completeness (all or nothing) is checked by the backend, which answers in German. */
export function smtpPayload(
	draft: SmtpDraft,
): { ok: true; value: SmtpPayload } | { ok: false; error: string } {
	const port = draft.port.trim();
	if (port && !/^\d+$/.test(port)) {
		return { ok: false, error: "Der Port muss eine ganze Zahl sein." };
	}
	return {
		ok: true,
		value: {
			host: draft.host.trim(),
			port: port ? Number(port) : null,
			security: draft.security || null,
			username: draft.username.trim(),
			password: draft.password,
			fromAddress: draft.fromAddress.trim(),
		},
	};
}

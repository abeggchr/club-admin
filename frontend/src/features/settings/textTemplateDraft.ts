export type TextTemplateDraft = {
	subject: string;
	text: string;
	greetings: string;
};

export const emptyTextTemplateDraft: TextTemplateDraft = {
	subject: "",
	text: "",
	greetings: "",
};

/** German error message, or null when all three fields are filled. */
export function validateTextTemplate(draft: TextTemplateDraft): string | null {
	if (!draft.subject.trim()) return "Betreff ist erforderlich.";
	if (!draft.text.trim()) return "Text ist erforderlich.";
	if (!draft.greetings.trim()) return "Grüsse sind erforderlich.";
	return null;
}

/** Placeholders usable in Betreff, Text and Grüsse; resolved when a Rechnung is created. */
export const textTemplatePlaceholders = [
	{ key: "name", description: "Name der Rechnungsempfänger:in" },
	{ key: "address", description: "Adresse der Rechnungsempfänger:in" },
	{ key: "amount", description: "Rechnungsbetrag" },
	{ key: "invoiceNumber", description: "Rechnungsnummer" },
	{ key: "invoiceDate", description: "Rechnungsdatum" },
] as const;

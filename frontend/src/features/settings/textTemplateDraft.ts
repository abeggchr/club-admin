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

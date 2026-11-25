/** How many unsent Invoices a bulk send delivers by each channel (from the preview endpoint). */
export type BulkSendPreview = {
	email: number;
	post: number;
	notSendable: number;
};

export type BulkSendOutcome = {
	number: string;
	person: string;
	/** Why it failed or was skipped; null for a sent Invoice. */
	reason: string | null;
};

export type BulkSendSummary = {
	sent: BulkSendOutcome[];
	failed: BulkSendOutcome[];
	skipped: BulkSendOutcome[];
};

export function bulkSendCounts(preview: BulkSendPreview): string {
	return `${preview.email} per E-Mail, ${preview.post} per Post, ${preview.notSendable} nicht versendbar`;
}

/** Subject and text are needed only when at least one Invoice goes by email. */
export function bulkSendNeedsMessage(preview: BulkSendPreview): boolean {
	return preview.email > 0;
}

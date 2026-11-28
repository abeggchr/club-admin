/** The Invoice Status as the backend serialises its InvoiceStatus enum. */
export type InvoiceStatus = "Created" | "Sent" | "Paid" | "Cancelled";

const statusLabels: Record<InvoiceStatus, string> = {
	Created: "Erstellt",
	Sent: "Versendet",
	Paid: "Bezahlt",
	Cancelled: "Storniert",
};

export function statusLabel(status: InvoiceStatus): string {
	return statusLabels[status];
}

/** Open means Created or Sent: the Invoice can still be paid, cancelled or sent (backend InvoiceStatusRules.IsOpen). */
export function isOpen(status: InvoiceStatus): boolean {
	return status === "Created" || status === "Sent";
}

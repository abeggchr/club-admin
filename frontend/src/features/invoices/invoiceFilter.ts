import type { InvoiceListItem } from "./api";
import { isOpen } from "./invoiceStatus";

/** The table views of the Invoices; Offene (the default) is the open ones, Nicht versendete only Created. */
export const invoiceFilters = {
	open: {
		label: "Offene",
		filter: (i: InvoiceListItem) => isOpen(i.status),
	},
	unsent: {
		label: "Nicht versendete",
		filter: (i: InvoiceListItem) => i.status === "Created",
	},
	paid: {
		label: "Bezahlte",
		filter: (i: InvoiceListItem) => i.status === "Paid",
	},
	cancelled: {
		label: "Stornierte",
		filter: (i: InvoiceListItem) => i.status === "Cancelled",
	},
	all: { label: "Alle", filter: () => true },
};

export type InvoiceFilterKey = keyof typeof invoiceFilters;

import type { InvoiceListItem } from "./api";

/** The table views of the Invoices; Offene (the default) is Created plus Sent. */
export const invoiceFilters = {
	open: {
		label: "Offene",
		filter: (i: InvoiceListItem) =>
			i.status === "Created" || i.status === "Sent",
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

export type Category = "Income" | "Expense";

export type Account = {
	number: number;
	name: string;
	category: Category;
	active: boolean;
};

export type Entry = {
	id: number;
	date: string;
	text: string;
	accountNumber: number;
	/** Signed from the bank's point of view: money in positive, money out negative. */
	amount: number;
};

/** A journal row: the Entry plus the running Expected Balance up to and including it. */
export type JournalEntry = Entry & { transactionId: number; balance: number };

/** A Transaction with the running balance after it; its Entries are the journal rows with the same transactionId. */
export type Transaction = {
	id: number;
	date: string;
	/** Signed from the bank's point of view: Deposit positive, Withdrawal negative. */
	amount: number;
	/** Optional free text describing this Transaction. */
	text: string | null;
	/** The optional Person this Transaction is linked to. */
	personId: number | null;
	/** The bank's reference of an imported Transaction; null for manual ones. */
	bankReference: string | null;
	/** The Ids of its Entries; empty means it is not booked yet ("Keine Buchung"). */
	entryIds: number[];
	/** The PDF Receipts (Belege) attached to it, in upload order. */
	receipts: Receipt[];
	balance: number;
};

export type Receipt = {
	id: number;
	/** UTC timestamp of the upload. */
	uploadedAt: string;
};

export type TransactionInput = {
	date: string;
	amount: number;
	text: string | null;
	personId: number | null;
	entries: {
		id: number | null;
		text: string;
		accountNumber: number;
		amount: number;
	}[];
};

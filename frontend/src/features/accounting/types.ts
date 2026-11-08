export type Category = 'Income' | 'Expense'

export type Account = {
  number: number
  name: string
  category: Category
  active: boolean
}

export type Entry = {
  id: number
  date: string
  text: string
  accountNumber: number
  /** Signed from the bank's point of view: money in positive, money out negative. */
  amount: number
}

/** A journal row: the Buchung plus the running Erwarteter Kontostand up to and including it. */
export type JournalEntry = Entry & { transactionId: number; balance: number }

/** A Bewegung with the Kontostand after it; its Buchungen are the journal rows with the same transactionId. */
export type Transaction = {
  id: number
  date: string
  /** Signed from the bank's point of view: Einzahlung positive, Auszahlung negative. */
  amount: number
  /** Optional free text describing this Bewegung. */
  text: string | null
  /** The optional Person this Bewegung is linked to. */
  personId: number | null
  /** The Ids of its Buchungen; empty means it is not booked yet ("Keine Buchung"). */
  entryIds: number[]
  balance: number
}

export type TransactionInput = {
  date: string
  amount: number
  text: string | null
  personId: number | null
  entries: { id: number | null; text: string; accountNumber: number; amount: number }[]
}

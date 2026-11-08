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
export type JournalEntry = Entry & { balance: number }

export type EntryInput = Omit<Entry, 'id'>

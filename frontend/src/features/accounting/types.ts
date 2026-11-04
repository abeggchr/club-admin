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

export type EntryInput = Omit<Entry, 'id'>

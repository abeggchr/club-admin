export type Category = 'Income' | 'Expense'

export type Account = {
  number: number
  name: string
  category: Category
  active: boolean
}

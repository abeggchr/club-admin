# The date lives on the Transaction; the balance is enforced above the database

A Transaction carries the booking date and the signed amount; its Entries have no date of their own and belong to exactly one Transaction. The bank statement shows one movement per line and one booking date, while the club's accounting splits that movement over several Accounts, so the date and the total belong to the movement and not to each split.

The rule that a Transaction has at least one Entry and that the signed amounts of its Entries sum to its amount is enforced in the edit panel and in the API (`TransactionRules`), not by the database.

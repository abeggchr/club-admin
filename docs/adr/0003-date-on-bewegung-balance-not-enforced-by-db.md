# The date lives on the transaction; the balance is enforced above the database

A transaction carries the booking date and the signed amount; its jounral entries have no date of their own and belong to exactly one transaction. The bank statement shows one movement per line and one booking date, while the club's accounting splits that movement over several Konten, so the date and the total belong to the movement and not to each split.

The rule that a transaction has at least one jounral entry and that the signed amounts of its entries sum to its amount is enforced in the edit panel and in the API (`TransactionRules`), not by the database. 

# The date lives on the Transaction; the balance is enforced above the database

A Transaction carries the booking date and the signed amount; its Entries have no date of their own and belong to exactly one Transaction. The bank statement shows one movement per line and one booking date, while the club's accounting splits that movement over several Accounts, so the date and the total belong to the movement and not to each split.

The rule that the signed amounts of a Transaction's Entries sum to its amount — once it has at least one — is enforced in the edit panel and in the API (`TransactionRules`), not by the database. A Transaction may have zero Entries, which means it is not booked yet ("Keine Buchung"): it counts in the bank's running Kontostand and adds nothing to the journal until Buchungen are added.

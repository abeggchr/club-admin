# The date lives on the Bewegung; the balance is enforced above the database

A Bewegung (_Transaction_) carries the booking date and the signed amount; its Buchungen (_Entry_) have no date of their own and belong to exactly one Bewegung. The bank statement shows one movement per line and one booking date, while the club's accounting splits that movement over several Konten, so the date and the total belong to the movement and not to each split.

The rule that a Bewegung has at least one Buchung and that the signed amounts of its Buchungen sum to its amount is enforced in the edit panel and in the API (`TransactionRules`), not by the database. The schema deliberately allows a Bewegung without Buchungen or with Buchungen that do not add up. We want to be able to store a Bewegung that is not yet assigned to Konten in a future feature (e.g. importing a bank statement, where the Bewegungen exist before anyone books them), and a database constraint would force that feature to invent placeholder Buchungen.

The cost is that the invariant can be broken by any writer that bypasses the API, and the journal's running balance (summed from Buchungen) can then differ from the Bewegung's running Kontostand (summed from Bewegung amounts). All writes therefore go through the API's rule module.

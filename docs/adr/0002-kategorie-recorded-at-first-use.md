# A Konto's Kategorie is recorded when a Buchung first uses it

ADR 0001 makes the app refuse to start when a used Konto changed its Kategorie, but a Buchung stores only its Konto number, so there is nothing to compare the file against. We store the Kategorie a Konto had when a Buchung first used it (one row per used Konto number) and compare the Kontoplan file against that record at startup. A Konto used before this record existed adopts the file's current Kategorie on the first startup after the change.

We rejected deriving the Kategorie from the sign of stored Buchungen: a refund on an Ausgaben-Konto is positive and a reversed income is negative, so signs do not reliably identify the Kategorie and would cause false startup failures. The cost of the record is one small table that must be written whenever a Konto is first used.

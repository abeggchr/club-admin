# Kontoplan is a checked-in file, not database data

The Kontoplan lives as a file in the repository and is read at startup. There is no Konten table and no UI to edit it; a Buchung stores only its Konto number. If the file and the existing Buchungen disagree (a Buchung references a Konto missing from the file, or a used Konto changed its Kategorie), the app refuses to start. A Konto is retired by marking it inactive in the file, never by removing it.

We chose this over an editable in-app Kontoplan because the plan changes rarely and a change then goes through review like any other code change. A failed health check on Railway keeps the previous deployment live, so an inconsistent edit never reaches production. The cost is that a change to the Kontoplan needs a deploy, and an inconsistency takes the whole app down, including the persons module, until it is fixed.

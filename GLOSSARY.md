# Glossary

Language of the club's revenue-expenses accounting (Einnahmen-Ausgaben-Rechnung), as opposed to double-entry bookkeeping. German terms are used in the UI, English terms in the code.

## Terms

**Kontoplan** (code: _ChartOfAccounts_):
The club's list of accounts that every Buchung is assigned to.

**Konto** (code: _Account_):
One account in the Kontoplan, identified by a number and a name (e.g. 3400 Jahresbeiträge), and belonging to one Kategorie. A Konto that has been used in a Buchung can be deactivated but never deleted.

**Kategorie** (code: _Category_):
The grouping of Konten in the Kontoplan: Einnahmen (_Income_) or Ausgaben (_Expense_). The Kategorie of a Buchung's Konto decides whether it is revenue or expense.

**Bewegung** (code: _Transaction_):
One money movement on the bank account: the bank's booking date and a signed amount, split into one or more Buchungen. The signed amounts of its Buchungen sum to its amount. Its direction follows from the sign of the amount and is not stored: it is an Einzahlung or an Auszahlung.

**Einzahlung** (code: a _Transaction_ with a positive amount):
A Bewegung where money comes in.

**Auszahlung** (code: a _Transaction_ with a negative amount):
A Bewegung where money goes out.

**Buchung** (code: _Entry_):
One line of a Bewegung: a text, a Konto and a signed amount (money in is positive, money out is negative). It belongs to exactly one Bewegung and has no date of its own; it takes the date of its Bewegung. Whether it is revenue or expense follows from the Kategorie of its Konto.

**Buchungsjournal** (code: _Journal_):
The chronological list of all Buchungen, ordered by the date of their Bewegung. It is the only record of money movements; it can be edited freely.

**Anfangssaldo** (code: _OpeningBalance_):
The bank account balance (an amount and a start date) from which the Erwarteter Kontostand is counted.

**Erwarteter Kontostand** (code: _ExpectedBalance_):
The amount the bank account should hold on a given date: the Anfangssaldo plus all Buchungen whose Bewegung is dated up to that date. The user compares it by eye against the bank statement.

**Textvorlage** (code: _TextTemplate_):
A reusable set of Betreff, Text and Grüsse that a Rechnung is written with. Managed under Einstellungen; all three fields are required. Zero Textvorlagen is a valid state.

**Rechnung** (code: _Invoice_):
A bill to one Person, created once and never changed or deleted afterwards. Its PDF is rendered at creation and stored; the Textvorlage texts live only in that PDF. It has no link to a Textvorlage and none to the Buchhaltung.

**Rechnungsposition** (code: _InvoiceLine_):
One line of a Rechnung: a text and an amount greater than 0. The total of a Rechnung is the sum of its Rechnungspositionen and is not stored.

**Rechnungsnummer**:
The ID of a Rechnung with at least four digits (0042, 12345). Gaps are possible when a creation fails.

**Rechnungsdatum**:
The date a Rechnung was created: the date of its Erstellt status change.

**Rechnungsstatus** (code: _InvoiceStatus_):
A lifecycle step of a Rechnung, recorded as an append-only status change with a timestamp. Currently only Erstellt (_Created_) exists.

## Relationships

- A Bewegung consists of one or more Buchungen; a Buchung belongs to exactly one Bewegung.
- A Buchung belongs to exactly one Konto.
- A Bewegung can optionally be linked to one Person; a Buchung is not linked to a Person.
- There is one bank account only; cash is not tracked.
- A Rechnung belongs to exactly one Person, has one or more Rechnungspositionen and one or more status changes.

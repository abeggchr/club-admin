# Glossary

Language of the club's revenue-expenses accounting (Einnahmen-Ausgaben-Rechnung), as opposed to double-entry bookkeeping. German terms are used in the UI, English terms in the code.

## Terms

**Kontoplan** (code: _ChartOfAccounts_):
The club's list of accounts that every Buchung is assigned to.

**Konto** (code: _Account_):
One account in the Kontoplan, identified by a number and a name (e.g. 3400 Jahresbeiträge), and belonging to one Kategorie. A Konto that has been used in a Buchung can be deactivated but never deleted.

**Kategorie** (code: _Category_):
The grouping of Konten in the Kontoplan: Einnahmen (_Income_) or Ausgaben (_Expense_). The Kategorie of a Buchung's Konto decides whether it is revenue or expense.

**Buchung** (code: _Entry_):
One entry in the Buchungsjournal: the bank's booking date, a text, a Konto and a signed amount (money in is positive, money out is negative). Whether it is revenue or expense follows from the Kategorie of its Konto.

**Buchungsjournal** (code: _Journal_):
The chronological list of all Buchungen. It is the only record of money movements; it can be edited freely.

**Anfangssaldo** (code: _OpeningBalance_):
The bank account balance (an amount and a start date) from which the Erwarteter Kontostand is counted.

**Erwarteter Kontostand** (code: _ExpectedBalance_):
The amount the bank account should hold on a given date: the Anfangssaldo plus all Buchungen up to that date. The user compares it by eye against the bank statement.

## Relationships

- A Buchung belongs to exactly one Konto.
- A Buchung is not linked to a Person.
- There is one bank account only; cash is not tracked.

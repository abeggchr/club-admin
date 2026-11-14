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
One money movement on the bank account: the bank's booking date and a signed amount, split into Buchungen. It may have zero Buchungen, in which case it is not booked yet and the table shows it as "Keine Buchung"; once it has at least one, the signed amounts of its Buchungen sum to its amount. Its direction follows from the sign of the amount and is not stored: it is an Einzahlung or an Auszahlung.

**Einzahlung** (code: a _Transaction_ with a positive amount):
A Bewegung where money comes in.

**Auszahlung** (code: a _Transaction_ with a negative amount):
A Bewegung where money goes out.

**Importierte Bewegung** (code: a _Transaction_ with a BankReference):
A Bewegung imported from the eBanking zip (CAMT.053 statements): the bank's booking date, the signed amount, the text and the BankReference. If the counterparty matched, the matching Person is linked as well. The date, amount, text and Person of an imported Bewegung are read-only afterwards, in the UI and the API; only its Buchungen may be edited. If the QR-bill reference of the payment matches an open Rechnung, the Bewegung arrives already carrying its Buchungen, derived from the Rechnung's positions; otherwise it has no Buchungen ("Keine Buchung"). A re-import skips a transaction whose BankReference is already stored.

**BankReference** (code: _BankReference_):
The bank's account server reference (AcctSvcrRef) of an imported booking. Unique per Bewegung and set only for imported ones, it is how the import recognizes a transaction that is already stored.

**Keine Buchung** (code: a _Transaction_ without Entries):
The state of a Bewegung without Buchungen: it is not booked yet. The Bewegungen table marks it with a "Keine Buchung" badge; it counts in the running Kontostand and adds nothing to the Buchungsjournal until Buchungen are added.

**Buchung** (code: _Entry_):
One line of a Bewegung: a text, a Konto and a signed amount (money in is positive, money out is negative). It belongs to exactly one Bewegung and has no date of its own; it takes the date of its Bewegung. Whether it is revenue or expense follows from the Kategorie of its Konto.

**Buchungsjournal** (code: _Journal_):
The chronological list of all Buchungen, ordered by the date of their Bewegung. It is the only record of money movements; it can be edited freely.

**Anfangssaldo** (code: _OpeningBalance_):
The bank account balance (an amount and a start date) from which the Erwarteter Kontostand is counted. The Anfangssaldo, its start date, the club's IBAN (Klub-IBAN, code: _Iban_) and the Klubadresse are stored together as the club settings (code: _Settings_); all are required.

**Klub-IBAN** (code: _Iban_):
The IBAN of the club's bank account (CH or LI, 21 characters, valid mod-97 checksum). The import checks each bank statement against it. A QR-IBAN (institution ID 30000-31999) is rejected.

**Klubadresse** (code: _Name_, _Address_, _PostalCode_ and _City_ in _Settings_):
The club's address as creditor of the QR-bill: Name, Adresse (one free-text line, e.g. "Sportweg 12"), PLZ and Ort. All four are required, with the QR-bill limits Name ≤ 70, Adresse ≤ 70, PLZ ≤ 16, Ort ≤ 35. The country is always CH. It is stored with the club settings and printed on every new Rechnung as the creditor of its QR-bill; creating a Rechnung without it is rejected.

**Erwarteter Kontostand** (code: _ExpectedBalance_):
The amount the bank account should hold on a given date: the Anfangssaldo plus all Buchungen whose Bewegung is dated up to that date. The user compares it by eye against the bank statement.

**Person** (code: _Person_):
A club member or contact: a name, membership dates and contact data. The address is stored as Strasse plus Hausnummer (code: _Street_ and _Number_), not as one line, so that imported bank addresses can be matched against it.

**Zahler** (code: _Payer_, _Person.PayerId_):
The Person who pays for another Person. Optional, set on the paid-for Person; the Zahler needs no active membership. There are no chains and no cycles: a Person cannot be their own Zahler, a Zahler has no Zahler, and a Person who is a Zahler cannot get one. A Person who is someone's Zahler cannot be deleted.

**Textvorlage** (code: _TextTemplate_):
A reusable set of Betreff, Text and Grüsse that a Rechnung is written with. Managed under Einstellungen; all three fields are required. The texts may contain Platzhalter, which are replaced when a Rechnung is created; an unknown Platzhalter is rejected on save. Zero Textvorlagen is a valid state.

**Rechnung** (code: _Invoice_):
A bill to one Person, created once and never changed or deleted afterwards. Its PDF is rendered at creation and stored; the Textvorlage texts are stored resolved (Platzhalter already replaced) and live only in that PDF. It has no link to a Textvorlage and no link to the Buchhaltung either: creating it writes no Buchung — each position's Konto is only a booking hint for the later Buchung (ADR 0004). The last page of the PDF ends with the QR-Rechnung payment part (210×105 mm): Klub-IBAN, Klubadresse, the Rechnung total, a SCOR reference (RF + check digits + Rechnungsnummer) and the message "Rechnung #0042"; the Person is the debtor only when their address is complete. "Betrag offen lassen" leaves the amount out of the QR code, so the payer enters it. If less than 105 mm is free on the last page, the payment part gets a page of its own.

**Rechnungsposition** (code: _InvoiceLine_):
One line of a Rechnung: a text, an amount greater than 0 and a required Konto from the Kontoplan, which is the booking hint for the later Buchung. The total of a Rechnung is the sum of its Rechnungspositionen and is not stored.

**Rechnungsnummer**:
The ID of a Rechnung with at least four digits (0042, 12345). Gaps are possible when a creation fails.

**Rechnungsdatum**:
The date a Rechnung was created: the date of its Erstellt status change.

**Rechnungsstatus** (code: _InvoiceStatus_):
A lifecycle step of a Rechnung, recorded as an append-only status change with a timestamp. Erstellt (_Created_) and Bezahlt (_Paid_, terminal).

**Bezahlt** (code: _Paid_ on _InvoiceStatus_):
A terminal Rechnungsstatus, recorded as a status change when the first imported Bewegung covers the Rechnung's total exactly. Its timestamp is the movement's booking date (shown in Europe/Zurich); once set, the Rechnung never matches another payment.

**PaidBy** (code: _PaidBy_ on _Invoice_):
The id of the Bewegung that first covered a Rechnung's total (null while open).

**Mitgliedschaft** (code: _MembershipType_):
The kind of membership of a Person (Familie, Einzel, Junior, Ehren, Frei, Gönner, or Unbekannt), which decides the Jahresbeitrag. Unbekannt is never invoiced.

**Jahresbeitrag** (code: _MembershipFeeRules_), plural **Jahresbeiträge**:
The yearly membership fee of a Person, set per Mitgliedschaft (a constant in the code) and booked to Konto 3400. "Jahresbeiträge" is also the run that creates the Rechnungen for all active Personen at once: one Rechnung per address with one Rechnungsposition per Person, where a Familie pays once per address (ADR 0005). A Rechnungsposition of 0 (an Ehren member, a further Familie member of the address, or a 100 % Ermässigung) is kept, but no Rechnung is created when its total is 0.

**Ermässigung** (code: _MembershipDiscount_):
A percentage (1-100) with a text that reduces one Person's Jahresbeitrag in the Jahresbeiträge run. It is stored per Person until removed, and its text is printed on the Rechnungsposition.

**Gläubigerreferenz** (code: _Reference_ on _Invoice_):
The Swiss QR-bill creditor reference (RF + check digits + Rechnungsnummer) stored at Rechnung creation. It is the sole key by which the bank import matches a payment to its Rechnung.

**Zuvielzahlung** (code: the extra Buchung to Konto 3600 on an overpayment):
When an imported payment exceeds the outstanding amount of its Rechnung, the excess is booked as an additional Buchung "Rechnung 0042" to Konto 3600 (Spenden).

## Relationships

- A Bewegung may have zero Buchungen (not booked yet, shown as "Keine Buchung") or one or more; once it has at least one, their signed amounts sum to its amount. A Buchung belongs to exactly one Bewegung.
- A Buchung belongs to exactly one Konto.
- A Bewegung can optionally be linked to one Person; a Buchung is not linked to a Person.
- There is one bank account only; cash is not tracked.
- A Person has at most one Zahler (another Person) and can be the Zahler of several Personen ("Zahler für"), but not both at once.
- A Rechnung belongs to exactly one Person, has one or more Rechnungspositionen and one or more status changes.
- A Rechnungsposition belongs to exactly one Konto of the Kontoplan (its booking hint).

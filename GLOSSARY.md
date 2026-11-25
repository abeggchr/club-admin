# Glossary

Language of the club's revenue-expenses accounting (Einnahmen-Ausgaben-Rechnung), as opposed to double-entry bookkeeping. The English terms are the ones used in code and code comments; the German term of each entry is the one shown in the UI. Strings that appear verbatim in the UI or in files stay German and are quoted.

## Terms

### Chart of Accounts & Bookkeeping

**Chart of Accounts** (German: _Kontoplan_):
The club's list of accounts that every Entry is assigned to.

**Account** (German: _Konto_):
One account in the Chart of Accounts, identified by a number and a name (e.g. 3400 Jahresbeiträge), and belonging to one Category. An Account that has been used in an Entry can be deactivated but never deleted.

**Category** (German: _Kategorie_):
The grouping of Accounts in the Chart of Accounts: Income (_Einnahmen_) or Expense (_Ausgaben_). The Category of an Entry's Account decides whether it is revenue or expense.

**Entry** (German: _Buchung_):
One line of a Transaction: a text, an Account and a signed amount (money in is positive, money out is negative). It belongs to exactly one Transaction and has no date of its own; it takes the date of its Transaction. Whether it is revenue or expense follows from the Category of its Account.

**Journal** (German: _Buchungsjournal_):
The chronological list of all Entries, ordered by the date of their Transaction. It is the only record of money movements; it can be edited freely.

**Opening Balance** (German: _Anfangssaldo_):
The bank account balance (an amount and a start date) from which the Expected Balance is counted. The Opening Balance, its start date, the club's Iban and the Club Address are stored together as the club Settings; all are required.

**Expected Balance** (German: _Erwarteter Kontostand_):
The amount the bank account should hold on a given date: the Opening Balance plus all Entries whose Transaction is dated up to that date. The user compares it by eye against the bank statement.

**Overpayment** (German: _Zuvielzahlung_):
When an imported payment exceeds the outstanding amount of its Invoice, the excess is booked as an additional Entry "Rechnung 0042" to Account 3600 (Spenden). The Invoice counts as covered and is marked Paid.

### Bank & Import

**Transaction** (German: _Bewegung_):
One money movement on the bank account: the bank's booking date and a signed amount, split into Entries. It may have zero Entries, in which case it is not booked yet and the table shows it as "Keine Buchung"; once it has at least one, the signed amounts of its Entries sum to its amount. Its direction follows from the sign of the amount and is not stored: it is a Deposit or a Withdrawal.

**Receipt** (German: _Beleg_):
A PDF attached to a Transaction, the paper that justifies the booking (a supplier's receipt, a payment confirmation). A Transaction has any number of them (at most 20), added at any time after it is saved, including an Imported or Unbooked one: attaching a Receipt changes none of its read-only bank facts. Stored in the same private bucket as the Invoice PDFs, but unlike those it can be removed, and it is deleted with its Transaction. A re-import and edits of the Entries leave Receipts untouched.

**Deposit** (German: _Einzahlung_):
A Transaction where money comes in (positive amount).

**Withdrawal** (German: _Auszahlung_):
A Transaction where money goes out (negative amount).

**Imported Transaction** (German: _Importierte Bewegung_):
A Transaction with a BankReference, imported from the eBanking zip or a single CAMT.053 XML file: the bank's booking date, the signed amount, the text and the BankReference. If the counterparty matched, the matching Person is linked as well. The date, amount, text and Person of an Imported Transaction are read-only afterwards, in the UI and the API; only its Entries may be edited. If the QR-bill reference of the payment matches an open Invoice, the Transaction arrives already carrying its Entries, derived from the Invoice's Lines; otherwise it has no Entries (Unbooked). A re-import skips a transaction whose BankReference is already stored.

**BankReference** (German: _Bankreferenz_):
The bank's account server reference (AcctSvcrRef) of an imported booking. Unique per Transaction and set only for imported ones, it is how the import recognizes a transaction that is already stored.

**Unbooked** (German: _Keine Buchung_):
The state of a Transaction without Entries: it is not booked yet. The Transactions table marks it with a "Keine Buchung" badge; it counts in the running balance (_Kontostand_) and adds nothing to the Journal until Entries are added.

**Iban** (German: _Klub-IBAN_):
The IBAN of the club's bank account (CH or LI, 21 characters, valid mod-97 checksum). The import checks each bank statement against it. A QR-IBAN (institution ID 30000-31999) is rejected. Stored with the club Settings (see Opening Balance).

### People & Membership

**Person** (German: _Person_):
A club member or contact: a name, membership dates and contact data. The address is stored as _Street_ plus _Number_ (German: Strasse, Hausnummer), not as one line, so that imported bank addresses can be matched against it. The flag _ReceivesClubNews_ (German: "Newsletter") marks a Person who receives the club news by snail mail even though they are no longer active.

**Mailing List** (German: _Versandliste_):
The list of addresses to send club news (by snail mail) to: every active Person (no exit date in the past; Membership Unknown counts) plus every Person with the ReceivesClubNews flag set. Recipients with the same complete address (street, number, postal code, city) share one row with their names, one per line; a recipient with an incomplete address gets a row of their own with whatever address parts exist. Downloaded from the Persons page as versandliste.csv (";"-separated, UTF-8 with BOM, columns Empfänger, Adresse, Ort, Land).

**Payer** (German: _Zahler_; code: _Person.PayerId_):
The Person who pays for another Person. Optional, set on the paid-for Person; the Payer needs no active membership. There are no chains and no cycles: a Person cannot be their own Payer, a Payer has no Payer, and a Person who is a Payer cannot get one. A Person who is someone's Payer cannot be deleted.

**Main Payer** (German: _Hauptzahler_; code: _IsMainPayer_ on _Person_):
A flag on a Person without Payer: they are the Recipient of their address and pay the Family fee in the Annual Fees run, ahead of the longest-standing Family member. In the UI it is one choice of the Payer picker, next to "Regulär" and the Persons. Choosing a named Payer clears the flag. A Person who is someone's Payer cannot get a Payer or the flag: the picker is locked for them.

**Membership** (German: _Mitgliedschaft_; code: _MembershipType_):
The kind of membership of a Person (Family, Single, Junior, Honorary, Free, Patron, or Unknown; German: Familie, Einzel, Junior, Ehren, Frei, Gönner, Unbekannt), which decides the Annual Fee. Unknown is never invoiced.

**Annual Fee** (German: _Jahresbeitrag_, plural _Jahresbeiträge_; code: _MembershipFeeRules_):
The yearly membership fee of a Person, set per Membership (a constant in the code) and booked to Account 3400. The "Annual Fees run" (German: "Jahresbeiträge") is also the run that creates the Invoices for all active Persons at once: one Invoice per address with one Line per Person, where a Family pays once per address and a Person with a Payer is billed on the Payer's Invoice (ADR 0005). A Line of 0 (an Honorary member, a further Family member of the address, or a 100 % Discount) is kept, but no Invoice is created when its total is 0.

**Discount** (German: _Ermässigung_; code: _MembershipDiscount_):
A percentage (1-100) with a text that reduces one Person's Annual Fee in the Annual Fees run. It is stored per Person until removed, and its text is printed on the Line.

### Invoices

**Club Address** (German: _Klubadresse_; code: _Name_, _Address_, _PostalCode_ and _City_ in _Settings_):
The club's address as creditor of the QR-bill: Name, Address (one free-text line, e.g. "Sportweg 12"), PostalCode and City. All four are required, with the QR-bill limits Name ≤ 70, Address ≤ 70, PostalCode ≤ 16, City ≤ 35. The country is always CH. It is stored with the club Settings (see Opening Balance) and printed on every new Invoice as the creditor of its QR-bill; creating an Invoice without it is rejected.

**Text Template** (German: _Textvorlage_; code: _TextTemplate_):
A reusable set of Subject, Text and Greetings (German: Betreff, Text, Grüsse) that an Invoice is written with. Managed under "Einstellungen"; all three fields are required. The texts may contain Placeholders, which are replaced when an Invoice is created; an unknown Placeholder is rejected on save. Zero Text Templates is a valid state.

**Placeholder** (German: _Platzhalter_):
A marker in a Text Template that is replaced by a value when an Invoice is created.

**Invoice** (German: _Rechnung_):
A bill to one Person (the Recipient; in the Annual Fees run the Payer of the Persons it covers), created once and never changed or deleted afterwards; its only changes are status changes: Sent (by hand, as often as wanted), Paid (by the import) or Cancelled (German: _storniert_, by hand, only while it is still Created or Sent; the Invoice is kept, but no longer counts as open and no longer matches imported payments). Its PDF is rendered at creation and stored; the Text Template texts are stored resolved (Placeholders already replaced) and live only in that PDF. It has no link to a Text Template and no link to the bookkeeping either: creating it writes no Entry — each Line's Account is only a booking hint for the later Entry (ADR 0004). The last page of the PDF ends with the QR-bill payment part (German: QR-Rechnung; 210×105 mm): Iban, Club Address, the Invoice total, a SCOR reference (RF + check digits + Invoice Number) and the message "Rechnung #0042"; the Person is the debtor only when their address is complete. "Betrag offen lassen" leaves the amount out of the QR code, so the payer enters it. If less than 105 mm is free on the last page, the payment part gets a page of its own.

**Recipient** (German: _Rechnungsempfänger_, _Empfänger_; code: _Recipient_):
The Person an Invoice is addressed to and who is the debtor of its QR-bill.

**Invoice Line** (German: _Rechnungsposition_; code: _InvoiceLine_):
One line of an Invoice: a text, an amount greater than 0 and a required Account from the Chart of Accounts, which is the booking hint for the later Entry. The total of an Invoice is the sum of its Lines and is not stored.

**Invoice Number** (German: _Rechnungsnummer_):
The ID of an Invoice with at least four digits (0042, 12345). Gaps are possible when a creation fails.

**Invoice Date** (German: _Rechnungsdatum_):
The date an Invoice was created: the date of its Created status change.

**Invoice Status** (German: _Rechnungsstatus_; code: _InvoiceStatus_):
A lifecycle step of an Invoice, recorded as an append-only status change with a timestamp. Created (German: Erstellt), Sent (German: Versendet) and Paid (German: Bezahlt, terminal). The current Invoice Status is the Paid or Cancelled status change if there is one, whatever the timestamps (a Paid row is timestamped with the Transaction's date and can predate a later one); otherwise the newest status change by timestamp, then id. The history is listed in timestamp order.

**Remark** (German: _Bemerkung_; code: _Remark_):
An optional plain-text comment on one Invoice status change, shown after its timestamp in the status history (e.g. "Versendet – 25.11.2026 14:03 – per Email").

**Sent** (German: _Versendet_; code: _Sent_ on _InvoiceStatus_):
An open, non-terminal Invoice Status, recorded once the Delivery Channel accepted the Invoice (for email: the SMTP server), with the Remark "per Email". Sending again adds another Sent row; a Sent Invoice can still be paid or cancelled and keeps matching imported payments. A failed send records nothing; a Paid or Cancelled Invoice cannot be sent.

**Delivery Channel** (German: _Versandart_; code: _DeliveryChannel_, _IInvoiceSender_):
The way an Invoice reaches its Recipient. Today only email (SMTP, configured under "Einstellungen"); it needs an email address on the Recipient. Each channel is a strategy that says whether it is configured, whether it can deliver to a Recipient, and sends the PDF; the _InvoiceDispatcher_ picks it and records Sent.

**Paid** (German: _Bezahlt_; code: _Paid_ on _InvoiceStatus_):
A terminal Invoice Status, recorded as a status change when the first Imported Transaction covers the Invoice's total (exactly or with an Overpayment), or when a Transaction is saved with the Invoice chosen in the Invoice picker of its form (the link is permanent). Its timestamp is the Transaction's date (shown in Europe/Zurich); once set, the Invoice never matches another payment.

**PaidBy** (code: _PaidBy_ on _Invoice_):
The id of the Transaction that paid an Invoice: the first to cover its total on import, or the one saved with the Invoice chosen in its form (null while open).

**Creditor Reference** (German: _Gläubigerreferenz_; code: _Reference_ on _Invoice_):
The Swiss QR-bill creditor reference (RF + check digits + Invoice Number) stored at Invoice creation. It is the sole key by which the bank import matches a payment to its Invoice.

## Relationships

- A Transaction may have zero Entries (not booked yet, shown as "Keine Buchung") or one or more; once it has at least one, their signed amounts sum to its amount. An Entry belongs to exactly one Transaction.
- An Entry belongs to exactly one Account.
- A Transaction can optionally be linked to one Person; an Entry is not linked to a Person.
- There is one bank account only; cash is not tracked.
- A Person has at most one Payer (another Person) and can be the Payer of several Persons (shown in the UI as "Zahler für"), but not both at once.
- An Invoice belongs to exactly one Person, has one or more Lines and one or more status changes.
- An Invoice Line belongs to exactly one Account of the Chart of Accounts (its booking hint).

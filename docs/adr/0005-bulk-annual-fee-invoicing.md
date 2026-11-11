# The Jahresbeiträge run creates one Rechnung per address and carries on past failures

The Jahresbeiträge run bills all active Personen in one go. The decisions behind it:

- **One Rechnung per address.** Personen sharing a complete address are grouped on one Rechnung with one Rechnungsposition each, including Personen who pay 0; a Person with an incomplete address gets their own Rechnung (and a warning). The recipient is the Familie payer, otherwise the longest-standing member. A Rechnung whose total is 0 is not created.
- **A Familie pays once.** Only the longest-standing Familie member of an address is billed the Familie fee; the other Familie members of that address get a Rechnungsposition of 0, so every member stays visible on the Rechnung. Lines of 0 are never dropped (Ehren, 100 % Ermässigung); an Ermässigung on a Person who pays 0 is ignored with a warning.
- **One transaction per Rechnung, failures do not stop the run.** Each Rechnung is created in its own transaction through the normal creation path (ADR 0004 still applies). A failed one is rolled back and listed in the summary; the others are kept. A rerun is therefore not atomic and can be repeated for what is missing.
- **No duplicate check.** The run does not look for an earlier Rechnung of the same year; the Kassier confirms before starting. Rechnungen are immutable and have no year, so there is nothing reliable to compare against.
- **Fees are constants.** The amounts per Mitgliedschaft live in the code (_MembershipFeeRules_), not in settings or the database; changing a fee is a code change. Only Ermässigungen are stored data.

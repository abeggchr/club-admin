# Income Statement layout is a checked-in YAML file

The Income Statement (Erfolgsrechnung) layout lives as a YAML file in the repository and is read at startup, like the Chart of Accounts (ADR 0001). The top-level key is `Erfolgsrechnung`, with the sections `Ertrag` and `Aufwand`; each section contains groups, and each group maps German labels to a single account number or a range written `from-to`. Key order is preserved and drives the A./B. section letters, the I./II./III. group numerals and the line order of the report. A malformed file refuses startup, so an inconsistent edit never reaches production.

We chose this over an editable in-app layout because the club reports the statement in a fixed structure, the change is rare, and a YAML diff in review is exactly the right granularity for a treasurer to understand. The cost is that a layout change needs a deploy, and a broken file takes the whole app down until it is fixed.

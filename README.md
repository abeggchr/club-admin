# Club Admin

> simple club administration, open-source

## Features

- Addresses (active members, former members, contacts)
  - Add/Edit
  - Import/Export
  - Download addresses grouped by the same address (Versand)
- Invoices
  - Create single invoices
  - Create batch invoices for yearly fee (grouped by address, other payer functionality)
- Accounting
  - Cash basis accounting (Einnahmen-Ausgaben-Rechnung)
- German user interface.

### Planned features

- Send invoices by email and snail mail
- Reporting
- Export of all data

## This is not for you

- your club has more than 1000 addresses
- you need customisation and don't know how to code
- you need support operating a webapp

## Stack

- Frontend: React
- Backend: C#
- DB: Postgres
- Object Storage: S3
- IAM: Azure Entra

## Getting started

The easiest way to get started is to [deploy](./docs/manual/deployment.md) to Railway
with switched off authentication. Ask your agent how to switch off authentication.
For local deployment option, see [deployment.md](./docs/manual/deployment.md).
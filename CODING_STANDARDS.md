# Coding Standards

Read during review (`/code-review`), not during implementation. These are judgement calls: rules a linter cannot enforce. Mechanical rules belong in a check, not here.

## Already enforced, do not review by hand

CI (`.github/workflows/ci.yml`) fails on: `dotnet format --verify-no-changes`, Biome (`npm run check`, tabs, double quotes, import order), `tsc --noEmit`, and all tests. Nullable reference types are on in the backend.

## Domain language

- Use the terms of `GLOSSARY.md` for types, members, variables, tests and comments: Entry, Transaction, Journal, Account, Category, Receipt, Invoice, Line. Flag a synonym ("booking", "payment line", "ledger") where a glossary term exists.
- Code and comments are English. Anything a user sees (validation messages, labels, errors) is German, using the German term from the glossary.
- Decisions that are hard to reverse or surprising are recorded in `docs/adr/`. A change that contradicts an ADR must supersede it or be rejected.

## Backend (C#, `backend/`)

- **Layout**: one folder per domain area (`Accounting`, `Invoicing`, `Person`, `Settings`). Namespaces follow folders. Do not add a cross-area type to `Shared` unless two areas already need it.
- **Business rules are pure.** Validation and calculation live in `*Rules`, `*Calculator` or similar static classes with no `DbContext`, clock or I/O. Controllers load data, call the rules, save, and map to DTOs; they hold no rule of their own.
- **A multi-step flow goes into a module** (`BankImporter`, `InvoiceDispatcher`, `TransactionEditor`) when it has several saves, partial failure or external calls. It takes the `DbContext` and ports (storage, senders, `TimeProvider`), calls the pure rules, saves, and returns sealed outcome records that the controller maps to HTTP in one switch. A simple load-call-save stays in the controller.
- **Validators return `string?`**: a German error message, or `null` when valid. Do not throw for a user input error.
- **External systems sit behind an interface** with a real and an in-memory implementation (`IReceiptStorage`: `S3ReceiptStorage`, `InMemoryReceiptStorage`). New I/O follows the same shape.
- **Time comes from outside**: the injected `TimeProvider` is the only clock, `InvoiceTime` gives Zurich dates. Do not read `DateTime.Now`, `DateTime.UtcNow` or `DateTime.Today`.
- **Money is `decimal`**, signed (in positive, out negative), never `double`. Dates without a time of day are `DateOnly`.
- **Controllers return DTO records**, not EF entities.
- **Migrations**: generated with `dotnet ef`, never hand-edited after they are merged. A schema change without a migration is a defect.
- **Comments** explain why, in one line, where the code surprises (see the Account-deactivated note in `EntryRules`). Do not narrate what the line does. Public rule types get a one-line `<summary>`.

## Backend tests (`backend.Tests/`)

- Mirror the source folder. Pure rules get plain unit tests; HTTP behaviour goes through `AppFactory` in `Api/`.
- **Test names are sentences** describing behaviour: `A_file_over_10_MB_is_rejected`. Not `Test1`, not `Validate_ReturnsError`.
- Test through the public interface: rule classes, endpoints. Do not test private helpers or assert on internals.
- Modules that take the `DbContext` are tested against a real database via `LocalPostgresFixture`; endpoint tests keep only wiring and JSON.
- A test needing a database uses `LocalPostgresFixture` and skips with a clear message when none is available. Never mock `DbContext`.
- Replace Entra with the test auth handler; do not add a production bypass (ADR 0006).

## Frontend (React, `frontend/src/`)

- **Layout**: routes in `routes/` stay thin and render a page from `features/<area>/`. Shared infrastructure lives in `lib/`.
- **All backend calls go through `apiFetch`** so a 401 redirects to login. Plain `fetch` is allowed only for the exceptions documented in `lib/auth.ts`.
- **Logic lives outside components.** Parsing, drafts, filters, selection and formatting are plain functions in sibling `.ts` files with a `.test.ts` next to them (`amount.ts`, `transactionDraft.ts`). A component with branching business logic should have it extracted.
- **Parse user input into a result type**, `{ ok: true, value } | { ok: false, error }`, as `parseAmount` does. Do not throw or return `NaN`.
- **Use `#/` imports** for anything outside the same folder.
- **Forms**: every input has a label bound to its control. A custom input component must be added to `inputComponents` in `biome.json`.
- **Do not edit** `routeTree.gen.ts`; regenerate it.
- UI strings are German and live where they are used. No i18n layer.

## Frontend tests

- Vitest, one test file per logic module, same naming style as the backend (behaviour sentences).
- Test the pure modules. Do not snapshot-test components.

## General

- Match the surrounding code: naming, comment density, file shape. When in doubt, copy the nearest sibling.
- No dead code, no commented-out code, no TODO without an issue number.
- A bug fix comes with a test that failed before the fix.
- Keep a change to one concern. A refactor and a behaviour change are separate commits.
- No new dependency without a reason in the PR description; this is a small club app and the stack is deliberately narrow.

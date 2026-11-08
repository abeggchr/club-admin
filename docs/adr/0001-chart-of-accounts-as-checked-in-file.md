# ChartOfAccounts is a checked-in file, not database data

The ChartOfAccounts lives as a file in the repository and is read at startup. There is no accounts table and no UI to edit it; an Entry stores only its account number. If the file and the existing Entries disagree (an Entry references an Account missing from the file, or a used Account changed its Category), the app refuses to start. An Account is retired by marking it inactive in the file, never by removing it.

We chose this over an editable in-app ChartOfAccounts because the plan changes rarely and a change then goes through review like any other code change. A failed health check on Railway keeps the previous deployment live, so an inconsistent edit never reaches production. The cost is that a change to the ChartOfAccounts needs a deploy, and an inconsistency takes the whole app down, including the persons module, until it is fixed.

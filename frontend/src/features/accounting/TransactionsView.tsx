import { useQuery } from "@tanstack/react-query";
import { useRef, useState } from "react";
import { MdAdd, MdFileUpload } from "react-icons/md";
import { entriesQueryOptions, transactionsQueryOptions } from "./api";
import { BalanceAtDate } from "./BalanceAtDate";
import { EditPanel } from "./EditPanel";
import { ImportDialog } from "./ImportDialog";
import { JournalList } from "./JournalList";
import { highlightOf, type Selection, toggleSelection } from "./selection";
import { TransactionsTable } from "./TransactionsTable";

/** What the panel beside the Transactions is doing: nothing, adding a new Transaction, or editing one. */
type PanelMode =
	| { kind: "closed" }
	| { kind: "add" }
	| { kind: "edit"; id: number; focusEntryId: number | null };

/** The Buchhaltung page: Transactions | edit panel (only while open) | Journal. */
export function TransactionsView() {
	const [mode, setMode] = useState<PanelMode>({ kind: "closed" });
	const [selection, setSelection] = useState<Selection | null>(null);
	const [dirty, setDirty] = useState(false);
	const importDialog = useRef<HTMLDialogElement>(null);
	const { data: transactions = [], isError: transactionsFailed } = useQuery(
		transactionsQueryOptions,
	);
	const { data: entries = [], isError: entriesFailed } =
		useQuery(entriesQueryOptions);
	const editing =
		mode.kind === "edit"
			? transactions.find((t) => t.id === mode.id)
			: undefined;
	const highlight = highlightOf(selection, entries);

	/** Selects a row (or deselects it when clicked again) and opens/closes the panel for its Transaction. */
	function onSelect(clicked: Selection) {
		const next = toggleSelection(selection, clicked);
		// Moving within the open Transaction keeps the draft; deselecting or switching to another one drops it.
		const keepsDraft =
			next !== null &&
			mode.kind === "edit" &&
			mode.id ===
				(clicked.kind === "transaction" ? clicked.id : clicked.transactionId);
		if (
			dirty &&
			!keepsDraft &&
			!window.confirm("Ungespeicherte Änderungen verwerfen?")
		)
			return;
		setSelection(next);
		if (!next) {
			setMode({ kind: "closed" });
		} else if (next.kind === "transaction") {
			setMode({ kind: "edit", id: next.id, focusEntryId: null });
		} else {
			setMode({ kind: "edit", id: next.transactionId, focusEntryId: next.id });
		}
	}

	return (
		<>
			<div className="mt-4 flex items-center justify-between gap-4">
				<div className="flex items-center gap-2">
					<button
						type="button"
						onClick={() => {
							setSelection(null);
							setMode({ kind: "add" });
						}}
						disabled={dirty}
						aria-label="Neue Bewegung"
						title={
							dirty
								? "Zuerst die ungespeicherten Änderungen speichern oder verwerfen"
								: "Neue Bewegung"
						}
						className="rounded bg-sky-600 p-2 text-white enabled:hover:bg-sky-700 disabled:opacity-40"
					>
						<MdAdd className="size-5" />
					</button>
					<button
						type="button"
						onClick={() => importDialog.current?.showModal()}
						disabled={dirty}
						title={
							dirty
								? "Zuerst die ungespeicherten Änderungen speichern oder verwerfen"
								: "Bewegungen aus der eBanking-ZIP importieren"
						}
						className="flex items-center gap-2 rounded border border-sky-600 px-3 py-2 text-sm text-sky-700 enabled:hover:bg-sky-50 disabled:opacity-40"
					>
						<MdFileUpload className="size-4" />
						Importieren
					</button>
				</div>
				<BalanceAtDate />
			</div>

			<ImportDialog dialogRef={importDialog} />

			{(transactionsFailed || entriesFailed) && (
				<p className="mt-4 text-red-600">
					Bewegungen konnten nicht geladen werden.
				</p>
			)}

			<div className="mt-4 flex items-start gap-4">
				<div className="flex flex-1">
					<TransactionsTable
						transactions={transactions}
						selectedId={selection?.kind === "transaction" ? selection.id : null}
						highlightedIds={highlight.transactionIds}
						onSelect={(t) => onSelect({ kind: "transaction", id: t })}
					/>
				</div>
				{(mode.kind === "add" || editing) && (
					<aside className="flex-1 shrink-0 rounded border border-slate-200 p-4">
						<EditPanel
							key={editing ? editing.id : "new"}
							transaction={editing}
							entries={
								editing
									? entries.filter((e) => e.transactionId === editing.id)
									: []
							}
							focusEntryId={mode.kind === "edit" ? mode.focusEntryId : null}
							onDone={() => setMode({ kind: "closed" })}
							onDeleted={() => setSelection(null)}
							onDirtyChange={setDirty}
						/>
					</aside>
				)}
				<div className="flex flex-1">
					<JournalList
						entries={entries}
						selectedId={selection?.kind === "entry" ? selection.id : null}
						highlightedIds={highlight.entryIds}
						onSelect={(e) =>
							onSelect({
								kind: "entry",
								id: e.id,
								transactionId: e.transactionId,
							})
						}
					/>
				</div>
			</div>
		</>
	);
}

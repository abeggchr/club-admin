import { useQueryClient, useSuspenseQuery } from "@tanstack/react-query";
import { type FormEvent, useEffect, useRef, useState } from "react";
import { MdClose } from "react-icons/md";
import { PersonPicker } from "../persons/PersonPicker";
import { formatChf } from "./amount";
import {
	accountsQueryOptions,
	deleteTransaction,
	invalidateJournalQueries,
	saveTransaction,
	settingsQueryOptions,
} from "./api";
import { KontoPicker } from "./KontoPicker";
import {
	addEntry,
	canDelete,
	canSave,
	type DraftErrors,
	draftFromTransaction,
	type EntryRow,
	emptyDraft,
	entryAmount,
	isDraftDirty,
	remainder,
	removeEntry,
	type TransactionDraft,
	toTransactionInput,
	validateDraft,
} from "./transactionDraft";
import type { JournalEntry, Transaction } from "./types";

function today(): string {
	const d = new Date();
	return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
}

const input = "rounded border border-slate-300 p-2";

type Props = {
	/** When set, the form edits this saved Bewegung (with its Buchungen) instead of creating a new one. */
	transaction?: Transaction;
	entries?: JournalEntry[];
	/** The Buchung whose row gets the focus when set or changed. */
	focusEntryId?: number | null;
	/** Closes the form: "Abbrechen", and automatically after saving. */
	onDone: () => void;
	/** Called after the Bewegung was deleted (the form closes afterwards). */
	onDeleted?: () => void;
	/** Reports whether the form holds unsaved changes. */
	onDirtyChange?: (dirty: boolean) => void;
};

export function EditPanel({
	transaction,
	entries = [],
	focusEntryId,
	onDone,
	onDeleted,
	onDirtyChange,
}: Props) {
	const queryClient = useQueryClient();
	const { data: accounts } = useSuspenseQuery(accountsQueryOptions);
	const { data: settings } = useSuspenseQuery(settingsQueryOptions);
	const [baseline] = useState<TransactionDraft>(() =>
		transaction
			? draftFromTransaction(transaction, entries, accounts)
			: emptyDraft(today()),
	);
	const [draft, setDraft] = useState(baseline);
	const [errors, setErrors] = useState<DraftErrors | null>(null);
	const [serverError, setServerError] = useState<string>();
	// The date, amount, text and person of an imported Bewegung are bank facts; the form shows them read-only.
	const imported = transaction?.bankReference != null;
	const dirty = isDraftDirty(baseline, draft, accounts);
	const rest = remainder(draft, accounts);
	const deletable = canDelete(draft);
	const savable = canSave(draft, accounts, settings?.startDate);
	const textInputs = useRef<(HTMLInputElement | null)[]>([]);
	// Latest draft for the focus effect, which must only re-run when the focused Buchung changes.
	const draftRef = useRef(draft);
	draftRef.current = draft;

	useEffect(() => {
		if (focusEntryId == null) return;
		const index = draftRef.current.entries.findIndex(
			(row) => row.id === focusEntryId,
		);
		textInputs.current[index]?.focus();
	}, [focusEntryId]);

	useEffect(() => {
		onDirtyChange?.(dirty);
		return () => onDirtyChange?.(false);
	}, [dirty, onDirtyChange]);

	function patchRow(index: number, patch: Partial<EntryRow>) {
		setDraft((d) => ({
			...d,
			entries: d.entries.map((row, i) =>
				i === index ? { ...row, ...patch } : row,
			),
		}));
	}

	async function onDelete() {
		if (!transaction) return;
		setServerError(undefined);
		try {
			await deleteTransaction(transaction.id);
			await invalidateJournalQueries(queryClient);
			onDeleted?.();
			onDone();
		} catch (err) {
			setServerError(
				err instanceof Error ? err.message : "Löschen fehlgeschlagen.",
			);
		}
	}

	async function onSubmit(e: FormEvent) {
		e.preventDefault();
		const found = validateDraft(draft, accounts, settings?.startDate);
		setErrors(found);
		setServerError(undefined);
		const body = found ? null : toTransactionInput(draft, accounts);
		if (!body) return;
		try {
			await saveTransaction(transaction?.id ?? null, body);
			await invalidateJournalQueries(queryClient);
			onDone();
		} catch (err) {
			setServerError(
				err instanceof Error ? err.message : "Speichern fehlgeschlagen.",
			);
		}
	}

	return (
		<form onSubmit={onSubmit} noValidate className="flex flex-col gap-3">
			<h3 className="font-medium">
				{transaction ? "Bewegung bearbeiten" : "Neue Bewegung"}
			</h3>
			<div className="flex gap-3">
				<label className="flex flex-1 flex-col text-sm">
					Datum
					<input
						type="date"
						value={draft.date}
						onChange={(e) => setDraft({ ...draft, date: e.target.value })}
						readOnly={imported}
						className={input}
					/>
					{errors?.date && <span className="text-red-600">{errors.date}</span>}
				</label>
				<label className="flex flex-1 flex-col text-sm">
					Betrag
					<input
						inputMode="decimal"
						value={draft.amount}
						onChange={(e) => setDraft({ ...draft, amount: e.target.value })}
						placeholder="Auszahlung mit -"
						readOnly={imported}
						className={`${input} text-right`}
					/>
					{errors?.amount && (
						<span className="text-red-600">{errors.amount}</span>
					)}
				</label>
			</div>

			<label className="flex flex-col text-sm">
				Person
				<PersonPicker
					value={draft.personId}
					onChange={(personId) => setDraft((d) => ({ ...d, personId }))}
					readOnly={imported}
					className={input}
				/>
			</label>

			<label className="flex flex-col text-sm">
				Text
				<input
					value={draft.text}
					maxLength={500}
					onChange={(e) => setDraft({ ...draft, text: e.target.value })}
					readOnly={imported}
					className={input}
				/>
			</label>

			{draft.entries.map((row, i) => (
				<fieldset
					// biome-ignore lint/suspicious/noArrayIndexKey: draft rows have no id; the index is their identity (patchRow, removeEntry)
					key={i}
					className="flex flex-col gap-2 rounded border border-slate-200 p-3"
				>
					<legend className="flex items-center gap-2 px-1 text-sm text-slate-500">
						Buchung
						<button
							type="button"
							onClick={() => setDraft((d) => removeEntry(d, i))}
							aria-label="Buchung entfernen"
							title="Buchung entfernen"
							className="rounded p-0.5 hover:bg-sky-100"
						>
							<MdClose className="size-4" />
						</button>
					</legend>
					<label className="flex flex-col text-sm">
						Text
						<input
							ref={(el) => {
								textInputs.current[i] = el;
							}}
							value={row.text}
							onChange={(e) => patchRow(i, { text: e.target.value })}
							className={input}
						/>
						{errors?.entries[i]?.text && (
							<span className="text-red-600">{errors.entries[i].text}</span>
						)}
					</label>
					<div className="flex gap-3">
						<label className="flex flex-[2] flex-col text-sm">
							Konto
							<KontoPicker
								value={row.accountNumber}
								onChange={(accountNumber) => patchRow(i, { accountNumber })}
								className={input}
							/>
							{errors?.entries[i]?.account && (
								<span className="text-red-600">
									{errors.entries[i].account}
								</span>
							)}
						</label>
						<label className="flex flex-1 flex-col text-sm">
							Betrag
							<input
								inputMode="decimal"
								value={entryAmount(draft, i, accounts)}
								onChange={(e) => patchRow(i, { amount: e.target.value })}
								className={`${input} text-right`}
							/>
							{errors?.entries[i]?.amount && (
								<span className="text-red-600">{errors.entries[i].amount}</span>
							)}
						</label>
					</div>
				</fieldset>
			))}
			<button
				type="button"
				onClick={() => setDraft((d) => addEntry(d, accounts))}
				className="self-start rounded border border-slate-300 px-3 py-1 text-sm hover:bg-sky-100"
			>
				+ Buchung
			</button>
			{draft.entries.length > 0 && rest !== null && (
				<p
					className={`text-sm ${rest === 0 ? "text-slate-600" : "font-medium text-red-700"}`}
				>
					Differenz {formatChf(rest)}
				</p>
			)}
			{errors?.remainder && <p className="text-red-600">{errors.remainder}</p>}

			<div className="flex justify-end gap-2">
				{transaction && (
					<button
						type="button"
						onClick={onDelete}
						disabled={!deletable}
						title={
							deletable ? "Bewegung löschen" : "Zuerst alle Buchungen entfernen"
						}
						className="mr-auto rounded border border-red-300 px-4 py-2 text-red-700 enabled:hover:bg-red-50 disabled:opacity-40"
					>
						Löschen
					</button>
				)}
				<button
					type="button"
					onClick={onDone}
					className="rounded border border-slate-300 hover:bg-sky-100 px-4 py-2"
				>
					Abbrechen
				</button>
				<button
					type="submit"
					disabled={!savable}
					className="rounded bg-sky-600 px-4 py-2 text-white enabled:hover:bg-sky-700 disabled:opacity-40"
				>
					{transaction ? "Speichern" : "Buchen"}
				</button>
			</div>
			{serverError && <p className="text-red-600">{serverError}</p>}
		</form>
	);
}

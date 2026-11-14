import { useQueryClient, useSuspenseQuery } from "@tanstack/react-query";
import { type FormEvent, useState } from "react";
import { MdClose } from "react-icons/md";
import { formatChf } from "../accounting/amount";
import { KontoPicker } from "../accounting/KontoPicker";
import { personsQueryOptions } from "../persons/api";
import { PersonPicker } from "../persons/PersonPicker";
import { personLabel } from "../persons/personFilter";
import { textTemplatesQueryOptions } from "../settings/textTemplateApi";
import { createInvoice, invalidateInvoices } from "./api";
import {
	addLine,
	emptyInvoiceDraft,
	type InvoiceDraft,
	type InvoiceDraftErrors,
	removeLine,
	toInvoiceInput,
	totalOf,
	validateInvoiceDraft,
} from "./invoiceDraft";
import { pickRecipient } from "./recipient";

const input = "rounded border border-slate-300 p-2";

export function CreatePanel({ onDone }: { onDone: () => void }) {
	const queryClient = useQueryClient();
	const { data: templates } = useSuspenseQuery(textTemplatesQueryOptions);
	const { data: persons } = useSuspenseQuery(personsQueryOptions);
	const [payerOf, setPayerOf] = useState<number | null>(null);
	const payerOfPerson = persons.find((p) => p.id === payerOf);
	const [draft, setDraft] = useState<InvoiceDraft>(emptyInvoiceDraft);
	const [errors, setErrors] = useState<InvoiceDraftErrors | null>(null);
	const [serverError, setServerError] = useState<string>();
	const [saving, setSaving] = useState(false);

	function patchLine(
		index: number,
		patch: Partial<InvoiceDraft["lines"][number]>,
	) {
		setDraft((d) => ({
			...d,
			lines: d.lines.map((l, i) => (i === index ? { ...l, ...patch } : l)),
		}));
	}

	async function onSubmit(e: FormEvent) {
		e.preventDefault();
		const found = validateInvoiceDraft(draft);
		setErrors(found);
		setServerError(undefined);
		const body = found ? null : toInvoiceInput(draft);
		if (!body) return;
		setSaving(true);
		try {
			await createInvoice(body);
			await invalidateInvoices(queryClient);
			onDone();
		} catch (err) {
			setServerError(
				err instanceof Error ? err.message : "Speichern fehlgeschlagen.",
			);
		} finally {
			setSaving(false);
		}
	}

	return (
		<form onSubmit={onSubmit} noValidate className="flex flex-col gap-3">
			<h3 className="font-medium">Neue Rechnung</h3>

			<label className="flex flex-col text-sm">
				Person
				<PersonPicker
					value={draft.personId}
					onChange={(picked) => {
						const { recipientId, payerOf } = pickRecipient(persons, picked);
						setPayerOf(payerOf);
						setDraft((d) => ({ ...d, personId: recipientId }));
					}}
					className={input}
					includeFormer
				/>
				{payerOfPerson && (
					<span className="text-slate-500">
						Zahler von {personLabel(payerOfPerson)}{" "}
						<button
							type="button"
							className="underline"
							onClick={() => {
								setPayerOf(null);
								setDraft((d) => ({ ...d, personId: payerOfPerson.id }));
							}}
						>
							Stattdessen {personLabel(payerOfPerson)} wählen
						</button>
					</span>
				)}
				{errors?.person && (
					<span className="text-red-600">{errors.person}</span>
				)}
			</label>

			<label className="flex flex-col text-sm">
				Textvorlage
				<select
					value={draft.templateId ?? ""}
					onChange={(e) =>
						setDraft((d) => ({
							...d,
							templateId: e.target.value === "" ? null : Number(e.target.value),
						}))
					}
					className={input}
				>
					<option value="">Textvorlage wählen…</option>
					{templates.map((t) => (
						<option key={t.id} value={t.id}>
							{t.subject}
						</option>
					))}
				</select>
				{errors?.template && (
					<span className="text-red-600">{errors.template}</span>
				)}
			</label>

			{draft.lines.map((line, i) => (
				<fieldset
					// biome-ignore lint/suspicious/noArrayIndexKey: draft rows have no id; the index is their identity (patchLine, removeLine)
					key={i}
					className="flex flex-col gap-2 rounded border border-slate-200 p-3"
				>
					<legend className="flex items-center gap-2 px-1 text-sm text-slate-500">
						Position
						<button
							type="button"
							onClick={() => setDraft((d) => removeLine(d, i))}
							aria-label="Position entfernen"
							title="Position entfernen"
							className="rounded p-0.5 hover:bg-sky-100"
						>
							<MdClose className="size-4" />
						</button>
					</legend>
					<div className="flex gap-3">
						<label className="flex flex-[2] flex-col text-sm">
							Konto
							<KontoPicker
								value={line.account}
								onChange={(account) => patchLine(i, { account })}
								className={input}
							/>
							{errors?.rows[i]?.account && (
								<span className="text-red-600">{errors.rows[i].account}</span>
							)}
						</label>
						<label className="flex flex-[2] flex-col text-sm">
							Text
							<input
								value={line.text}
								onChange={(e) => patchLine(i, { text: e.target.value })}
								className={input}
							/>
							{errors?.rows[i]?.text && (
								<span className="text-red-600">{errors.rows[i].text}</span>
							)}
						</label>
						<label className="flex flex-1 flex-col text-sm">
							Betrag
							<input
								inputMode="decimal"
								value={line.amount}
								onChange={(e) => patchLine(i, { amount: e.target.value })}
								className={`${input} text-right`}
							/>
							{errors?.rows[i]?.amount && (
								<span className="text-red-600">{errors.rows[i].amount}</span>
							)}
						</label>
					</div>
				</fieldset>
			))}
			{errors?.lines && <p className="text-red-600">{errors.lines}</p>}
			<button
				type="button"
				onClick={() => setDraft(addLine)}
				className="self-start rounded border border-slate-300 px-3 py-1 text-sm hover:bg-sky-100"
			>
				+ Position
			</button>
			<p className="text-right font-medium">
				Total {formatChf(totalOf(draft.lines))}
			</p>

			<label className="flex items-center gap-2 text-sm">
				<input
					type="checkbox"
					checked={draft.openAmount}
					onChange={(e) =>
						setDraft((d) => ({ ...d, openAmount: e.target.checked }))
					}
				/>
				Betrag offen lassen
			</label>

			<div className="flex justify-end gap-2">
				<button
					type="button"
					onClick={onDone}
					className="rounded border border-slate-300 px-4 py-2 hover:bg-sky-100"
				>
					Abbrechen
				</button>
				<button
					type="submit"
					disabled={saving}
					className="rounded bg-sky-600 px-4 py-2 text-white enabled:hover:bg-sky-700 disabled:opacity-40"
				>
					Speichern
				</button>
			</div>
			{serverError && <p className="text-red-600">{serverError}</p>}
		</form>
	);
}

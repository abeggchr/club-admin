import {
	useMutation,
	useQueryClient,
	useSuspenseQuery,
} from "@tanstack/react-query";
import { useState } from "react";
import { textTemplatesQueryOptions } from "../settings/textTemplateApi";
import { createAnnualFeeInvoices, invalidateInvoices } from "./api";
import { DiscountsSection } from "./DiscountsSection";

/** Jahresbeiträge: creates the Rechnungen for all active members with one Textvorlage. */
export function AnnualFeesDialog({
	dialogRef,
}: {
	dialogRef: React.RefObject<HTMLDialogElement | null>;
}) {
	const queryClient = useQueryClient();
	const { data: templates } = useSuspenseQuery(textTemplatesQueryOptions);
	const [templateId, setTemplateId] = useState<number | null>(null);
	const selected = templateId ?? templates[0]?.id ?? null;
	const run = useMutation({
		mutationFn: createAnnualFeeInvoices,
		onSettled: () => invalidateInvoices(queryClient),
	});
	const busy = run.isPending;
	const summary = run.data;

	function submit(e: React.FormEvent) {
		e.preventDefault();
		if (selected === null) return;
		if (
			!window.confirm(
				"Es werden Rechnungen für alle aktiven Mitglieder erstellt. Das kann zu Duplikaten führen. Fortfahren?",
			)
		)
			return;
		run.mutate(selected);
	}

	return (
		<dialog
			ref={dialogRef}
			onClose={() => run.reset()}
			className="m-auto w-2xl rounded border border-neutral-300 p-6 backdrop:bg-black/40"
		>
			<form onSubmit={submit}>
				<h2 className="text-xl font-bold">Jahresbeiträge</h2>
				<label className="mt-4 block text-sm">
					Textvorlage
					<select
						required
						value={selected ?? ""}
						onChange={(e) => setTemplateId(Number(e.target.value))}
						disabled={busy}
						className="mt-1 block w-full rounded border border-slate-300 p-2"
					>
						{templates.map((t) => (
							<option key={t.id} value={t.id}>
								{t.subject}
							</option>
						))}
					</select>
				</label>
				<DiscountsSection disabled={busy} />
				{run.error && (
					<p className="mt-3 text-sm text-red-600">{run.error.message}</p>
				)}
				{summary && (
					<div className="mt-3 text-sm">
						<p className="text-green-700">
							{summary.created} Rechnungen erstellt.
						</p>
						<MessageList
							title={`${summary.failed.length} fehlgeschlagen:`}
							items={summary.failed}
							tone="red"
						/>
						<MessageList
							title="Warnungen:"
							items={summary.warnings}
							tone="amber"
						/>
					</div>
				)}
				<div className="mt-6 flex justify-end gap-2 text-sm">
					<button
						type="button"
						onClick={() => dialogRef.current?.close()}
						disabled={busy}
						className="rounded border border-neutral-300 px-3 py-1 disabled:opacity-40"
					>
						{summary ? "Schliessen" : "Abbrechen"}
					</button>
					<button
						type="submit"
						disabled={busy || selected === null}
						className="rounded bg-sky-600 px-3 py-1 text-white disabled:opacity-40"
					>
						{busy ? "Erstelle…" : "Rechnungen erstellen"}
					</button>
				</div>
			</form>
		</dialog>
	);
}

const tones = {
	red: { title: "text-red-600", list: "text-red-600" },
	amber: { title: "text-amber-700", list: "text-amber-700" },
};

function MessageList({
	title,
	items,
	tone,
}: {
	title: string;
	items: string[];
	tone: keyof typeof tones;
}) {
	if (items.length === 0) return null;
	return (
		<>
			<p className={`mt-2 font-medium ${tones[tone].title}`}>{title}</p>
			<ul className={`list-disc pl-5 ${tones[tone].list}`}>
				{items.map((i) => (
					<li key={i}>{i}</li>
				))}
			</ul>
		</>
	);
}

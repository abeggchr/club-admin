import {
	useMutation,
	useQueryClient,
	useSuspenseQuery,
} from "@tanstack/react-query";
import { useId, useState } from "react";
import { settingsQueryOptions } from "../accounting/api";
import {
	bulkSendInvoices,
	bulkSendPreviewQueryOptions,
	invalidateInvoices,
} from "./api";
import { bulkSendCounts, bulkSendNeedsMessage } from "./bulkSend";

/** Sends all unsent Invoices at once: the channel is picked per Invoice; subject and text are asked for only when some go by email. */
export function BulkSendDialog({
	dialogRef,
}: {
	dialogRef: React.RefObject<HTMLDialogElement | null>;
}) {
	const queryClient = useQueryClient();
	const subjectId = useId();
	const textId = useId();
	const { data: preview } = useSuspenseQuery(bulkSendPreviewQueryOptions);
	const { data: settings } = useSuspenseQuery(settingsQueryOptions);
	const [subject, setSubject] = useState("");
	const [text, setText] = useState("");
	const run = useMutation({
		mutationFn: bulkSendInvoices,
		onSettled: () => invalidateInvoices(queryClient),
	});
	const busy = run.isPending;
	const summary = run.data;
	const needsMessage = bulkSendNeedsMessage(preview);
	const nothingToSend = preview.email + preview.post === 0;

	return (
		<dialog
			ref={dialogRef}
			onClose={() => {
				run.reset();
				setSubject("");
				setText("");
			}}
			className="m-auto w-2xl rounded border border-neutral-300 p-6 backdrop:bg-black/40"
		>
			<form
				onSubmit={(e) => {
					e.preventDefault();
					run.mutate({ subject, text });
				}}
			>
				<h2 className="text-xl font-bold">Rechnungen versenden</h2>
				{!summary && (
					<>
						<p className="mt-4 text-sm">
							Alle noch nicht versendeten Rechnungen: {bulkSendCounts(preview)}.
						</p>
						{needsMessage && (
							<>
								<p className="mt-3 text-sm text-neutral-600">
									Betreff und Text gehen an alle Personen, die per E-Mail
									beliefert werden.
									{settings?.emailBackend === "EPost" &&
										" E-Mails gehen über ePost. Jeder Versand wird verrechnet."}
								</p>
								<label htmlFor={subjectId} className="mt-3 block text-sm">
									Betreff
								</label>
								<input
									id={subjectId}
									type="text"
									required
									value={subject}
									onChange={(e) => setSubject(e.target.value)}
									disabled={busy}
									className="mt-1 block w-full rounded border border-neutral-300 px-2 py-1"
								/>
								<label htmlFor={textId} className="mt-3 block text-sm">
									Text
								</label>
								<textarea
									id={textId}
									required
									rows={8}
									value={text}
									onChange={(e) => setText(e.target.value)}
									disabled={busy}
									className="mt-1 block w-full rounded border border-neutral-300 px-2 py-1"
								/>
							</>
						)}
						{preview.post > 0 && (
							<p className="mt-3 text-sm text-neutral-600">
								Briefe gehen über ePost (B-Post). Jeder Versand wird verrechnet.
							</p>
						)}
					</>
				)}
				{run.error && (
					<p className="mt-3 text-sm text-red-600">{run.error.message}</p>
				)}
				{summary && (
					<div className="mt-3 text-sm">
						<p className="text-green-700">
							{summary.sent.length} Rechnungen versendet.
						</p>
						<OutcomeList
							title={`${summary.failed.length} fehlgeschlagen:`}
							items={summary.failed}
							className="text-red-600"
						/>
						<OutcomeList
							title={`${summary.skipped.length} nicht versendbar:`}
							items={summary.skipped}
							className="text-amber-700"
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
					{!summary && (
						<button
							type="submit"
							disabled={busy || nothingToSend}
							className="rounded bg-sky-600 px-3 py-1 text-white disabled:opacity-40"
						>
							{busy ? "Versende…" : "Versenden"}
						</button>
					)}
				</div>
			</form>
		</dialog>
	);
}

function OutcomeList({
	title,
	items,
	className,
}: {
	title: string;
	items: { number: string; person: string; reason: string | null }[];
	className: string;
}) {
	if (items.length === 0) return null;
	return (
		<>
			<p className={`mt-2 font-medium ${className}`}>{title}</p>
			<ul className={`list-disc pl-5 ${className}`}>
				{items.map((i) => (
					<li key={i.number}>
						{i.number} {i.person}: {i.reason}
					</li>
				))}
			</ul>
		</>
	);
}

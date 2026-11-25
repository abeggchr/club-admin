import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useId, useState } from "react";
import { invalidateInvoices, sendInvoice } from "./api";

/** Asks for the subject and text (both required, plain text) and emails the Invoice PDF. */
export function SendDialog({
	dialogRef,
	invoiceId,
	recipientEmail,
}: {
	dialogRef: React.RefObject<HTMLDialogElement | null>;
	invoiceId: number;
	recipientEmail: string;
}) {
	const queryClient = useQueryClient();
	const subjectId = useId();
	const textId = useId();
	const [subject, setSubject] = useState("");
	const [text, setText] = useState("");
	const send = useMutation({
		mutationFn: () => sendInvoice(invoiceId, { subject, text }),
		onSuccess: () => {
			dialogRef.current?.close();
			return invalidateInvoices(queryClient);
		},
	});

	function reset() {
		setSubject("");
		setText("");
		send.reset();
	}

	return (
		<dialog
			ref={dialogRef}
			onClose={reset}
			className="m-auto w-[32rem] rounded border border-neutral-300 p-6 backdrop:bg-black/40"
		>
			<form
				onSubmit={(e) => {
					e.preventDefault();
					send.mutate();
				}}
			>
				<h2 className="text-xl font-bold">Rechnung versenden</h2>
				<p className="mt-1 text-sm text-neutral-600">
					Die Rechnung wird als PDF an {recipientEmail} gesendet.
				</p>
				<label htmlFor={subjectId} className="mt-4 block text-sm">
					Betreff
				</label>
				<input
					id={subjectId}
					type="text"
					required
					value={subject}
					onChange={(e) => setSubject(e.target.value)}
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
					className="mt-1 block w-full rounded border border-neutral-300 px-2 py-1"
				/>
				{send.isError && (
					<p className="mt-3 text-sm text-red-600">{send.error.message}</p>
				)}
				<div className="mt-6 flex justify-end gap-2 text-sm">
					<button
						type="button"
						onClick={() => dialogRef.current?.close()}
						className="rounded border border-neutral-300 px-3 py-1"
					>
						Abbrechen
					</button>
					<button
						type="submit"
						disabled={send.isPending}
						className="rounded bg-sky-600 px-3 py-1 text-white disabled:opacity-40"
					>
						{send.isPending ? "Sende…" : "Versenden"}
					</button>
				</div>
			</form>
		</dialog>
	);
}

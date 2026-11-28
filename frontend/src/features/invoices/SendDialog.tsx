import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useId, useState } from "react";
import type { EmailBackend } from "../accounting/api";
import { invalidateInvoices, sendInvoice } from "./api";
import {
	type ChannelOption,
	channelLabel,
	type DeliveryChannel,
	preselectedChannel,
} from "./invoiceDetail";

/** Lets the treasurer choose the Delivery Channel; an email asks for subject and text (both required, plain text), a letter needs nothing more. */
export function SendDialog({
	dialogRef,
	invoiceId,
	channels,
	recipientEmail,
	emailBackend,
}: {
	dialogRef: React.RefObject<HTMLDialogElement | null>;
	invoiceId: number;
	channels: ChannelOption[];
	recipientEmail: string;
	/** Who delivers emails; ePost emails are billed, SMTP ones are not. */
	emailBackend: EmailBackend | null;
}) {
	const queryClient = useQueryClient();
	const subjectId = useId();
	const textId = useId();
	const [subject, setSubject] = useState("");
	const [text, setText] = useState("");
	const [chosen, setChosen] = useState<DeliveryChannel | null>(null);
	const channel =
		channels.find((o) => o.channel === chosen && o.blockedReason === null)
			?.channel ?? preselectedChannel(channels);
	const send = useMutation({
		mutationFn: () => sendInvoice(invoiceId, { channel, subject, text }),
		onSuccess: () => {
			dialogRef.current?.close();
			return invalidateInvoices(queryClient);
		},
	});

	function reset() {
		setSubject("");
		setText("");
		setChosen(null);
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
				<fieldset className="mt-3">
					<legend className="text-sm">Versandart</legend>
					{channels.map((option) => (
						<div key={option.channel} className="mt-1 text-sm">
							<label
								className={
									option.blockedReason ? "text-neutral-400" : undefined
								}
							>
								<input
									type="radio"
									name="channel"
									value={option.channel}
									checked={channel === option.channel}
									disabled={option.blockedReason !== null}
									onChange={() => setChosen(option.channel)}
									className="mr-2"
								/>
								{channelLabel(option.channel)}
							</label>
							{option.blockedReason && (
								<span className="ml-2 text-neutral-500">
									({option.blockedReason})
								</span>
							)}
						</div>
					))}
				</fieldset>
				{channel === "Post" ? (
					<p className="mt-3 text-sm text-neutral-600">
						Die Rechnung wird als Brief (B-Post) über ePost versendet. Jeder
						Versand wird verrechnet.
					</p>
				) : (
					<>
						<p className="mt-3 text-sm text-neutral-600">
							Die Rechnung wird als PDF an {recipientEmail} gesendet
							{emailBackend === "EPost"
								? " (über ePost, kostenpflichtig)."
								: "."}
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
					</>
				)}
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
						disabled={send.isPending || channel === null}
						className="rounded bg-sky-600 px-3 py-1 text-white disabled:opacity-40"
					>
						{send.isPending ? "Sende…" : "Versenden"}
					</button>
				</div>
			</form>
		</dialog>
	);
}

import { useQueryClient } from "@tanstack/react-query";
import { type ChangeEvent, useState } from "react";
import { MdClose } from "react-icons/md";
import {
	deleteReceipt,
	invalidateJournalQueries,
	receiptUrl,
	uploadReceipt,
} from "./api";
import { receiptLabel } from "./receipts";
import type { Transaction } from "./types";

type Props = {
	transaction: Transaction;
};

/** The Receipts (Belege) of a saved Transaction: open, remove, and add several PDFs at once. */
export function ReceiptsSection({ transaction }: Props) {
	const queryClient = useQueryClient();
	const [errors, setErrors] = useState<string[]>([]);

	async function onFiles(e: ChangeEvent<HTMLInputElement>) {
		const input = e.target;
		const files = Array.from(input.files ?? []);
		input.value = "";
		// One request per file, so a bad file fails on its own and the good ones are still attached.
		const failed: string[] = [];
		for (const file of files) {
			try {
				await uploadReceipt(transaction.id, file);
			} catch (err) {
				failed.push(
					`${file.name}: ${err instanceof Error ? err.message : "Hochladen fehlgeschlagen."}`,
				);
			}
		}
		setErrors(failed);
		await invalidateJournalQueries(queryClient);
	}

	async function onRemove(id: number) {
		try {
			await deleteReceipt(id);
			setErrors([]);
		} catch (err) {
			setErrors([
				err instanceof Error ? err.message : "Entfernen fehlgeschlagen.",
			]);
		}
		await invalidateJournalQueries(queryClient);
	}

	return (
		<section className="flex flex-col gap-2 rounded border border-slate-200 p-3">
			<h4 className="text-sm text-slate-500">Belege</h4>
			{transaction.receipts.length > 0 && (
				<ul className="flex flex-col gap-1 text-sm">
					{transaction.receipts.map((receipt, i) => (
						<li key={receipt.id} className="flex items-center gap-2">
							<a
								href={receiptUrl(receipt.id)}
								target="_blank"
								rel="noreferrer"
								className="text-sky-700 underline"
							>
								{receiptLabel(i, receipt.uploadedAt)}
							</a>
							<button
								type="button"
								onClick={() => onRemove(receipt.id)}
								aria-label="Beleg entfernen"
								title="Beleg entfernen"
								className="rounded p-0.5 hover:bg-sky-100"
							>
								<MdClose className="size-4" />
							</button>
						</li>
					))}
				</ul>
			)}
			<label className="flex flex-col text-sm">
				<span className="mb-1">PDF hinzufügen</span>
				<input
					type="file"
					accept="application/pdf"
					multiple
					onChange={onFiles}
					className="text-sm"
				/>
			</label>
			{errors.map((error) => (
				<p key={error} className="text-sm text-red-600">
					{error}
				</p>
			))}
		</section>
	);
}

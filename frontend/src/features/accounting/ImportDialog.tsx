import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import {
	importTransactions,
	invalidateJournalQueries,
	type TransactionImportError,
	TransactionImportFailed,
	type TransactionImportResult,
} from "./api";

export function ImportDialog({
	dialogRef,
}: {
	dialogRef: React.RefObject<HTMLDialogElement | null>;
}) {
	const queryClient = useQueryClient();
	const [file, setFile] = useState<File | null>(null);
	const [error, setError] = useState<string | null>(null);
	const [errors, setErrors] = useState<TransactionImportError[] | null>(null);
	const [result, setResult] = useState<TransactionImportResult | null>(null);
	const upload = useMutation({
		mutationFn: importTransactions,
		onSuccess: (res) => {
			setResult(res);
			return invalidateJournalQueries(queryClient);
		},
		onError: (err) => {
			if (err instanceof TransactionImportFailed) setErrors(err.errors);
			else setError(err.message);
		},
	});
	const busy = upload.isPending;

	function reset() {
		setFile(null);
		setError(null);
		setErrors(null);
		setResult(null);
	}

	function submit(e: React.FormEvent) {
		e.preventDefault();
		if (!file) return;
		setError(null);
		setErrors(null);
		upload.mutate(file);
	}

	return (
		<dialog
			ref={dialogRef}
			onClose={reset}
			className="m-auto w-[28rem] rounded border border-neutral-300 p-6 backdrop:bg-black/40"
		>
			<form onSubmit={submit}>
				<h2 className="text-xl font-bold">Bewegungen importieren</h2>
				<p className="mt-1 text-sm text-neutral-600">
					Die ZIP-Datei aus dem eBanking (CAMT.053) wird direkt hochgeladen.
				</p>
				<input
					type="file"
					accept=".zip,application/zip"
					onChange={(e) => {
						setFile(e.target.files?.[0] ?? null);
						setError(null);
						setErrors(null);
						setResult(null);
					}}
					className="mt-4 block w-full text-sm"
				/>
				{error && <p className="mt-3 text-sm text-red-600">{error}</p>}
				{errors && (
					<ul className="mt-3 list-disc pl-5 text-sm text-red-600">
						{errors.map((e, i) => (
							<li key={i}>{e.file ? `${e.file}: ${e.message}` : e.message}</li>
						))}
					</ul>
				)}
				{result && (
					<p className="mt-3 text-sm text-green-700">
						{result.imported} importiert, {result.skippedDuplicates} als
						Duplikat übersprungen.
					</p>
				)}
				<div className="mt-6 flex justify-end gap-2 text-sm">
					<button
						type="button"
						onClick={() => dialogRef.current?.close()}
						className="rounded border border-neutral-300 px-3 py-1"
					>
						{result ? "Schliessen" : "Abbrechen"}
					</button>
					<button
						type="submit"
						disabled={!file || busy || result !== null}
						className="rounded bg-blue-600 px-3 py-1 text-white disabled:opacity-40"
					>
						{busy ? "Importiere…" : "Importieren"}
					</button>
				</div>
			</form>
		</dialog>
	);
}

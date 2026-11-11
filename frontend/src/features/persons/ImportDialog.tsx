import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { importPersons, personsQueryOptions } from "./api";

export function ImportDialog({
	dialogRef,
}: {
	dialogRef: React.RefObject<HTMLDialogElement | null>;
}) {
	const queryClient = useQueryClient();
	const [file, setFile] = useState<File | null>(null);
	const [error, setError] = useState<string | null>(null);
	const [imported, setImported] = useState<number | null>(null);
	const upload = useMutation({
		mutationFn: importPersons,
		onSuccess: (count) => {
			setImported(count);
			return queryClient.invalidateQueries({
				queryKey: personsQueryOptions.queryKey,
			});
		},
		onError: (err) => setError(err.message),
	});
	const busy = upload.isPending;

	function reset() {
		setFile(null);
		setError(null);
		setImported(null);
	}

	function submit(e: React.FormEvent) {
		e.preventDefault();
		if (!file) return;
		setError(null);
		upload.mutate(file);
	}

	return (
		<dialog
			ref={dialogRef}
			onClose={reset}
			className="m-auto w-[28rem] rounded border border-neutral-300 p-6 backdrop:bg-black/40"
		>
			<form onSubmit={submit}>
				<h2 className="text-xl font-bold">Personen importieren</h2>
				<input
					type="file"
					accept=".csv,text/csv"
					onChange={(e) => {
						setFile(e.target.files?.[0] ?? null);
						setError(null);
						setImported(null);
					}}
					className="mt-4 block w-full text-sm"
				/>
				{error && <p className="mt-3 text-sm text-red-600">{error}</p>}
				{imported !== null && (
					<p className="mt-3 text-sm text-green-700">
						{imported} Personen importiert.
					</p>
				)}
				<div className="mt-6 flex justify-end gap-2 text-sm">
					<button
						type="button"
						onClick={() => dialogRef.current?.close()}
						className="rounded border border-neutral-300 px-3 py-1"
					>
						{imported !== null ? "Schliessen" : "Abbrechen"}
					</button>
					<button
						type="submit"
						disabled={!file || busy || imported !== null}
						className="rounded bg-blue-600 px-3 py-1 text-white disabled:opacity-40"
					>
						{busy ? "Importiere…" : "Importieren"}
					</button>
				</div>
			</form>
		</dialog>
	);
}

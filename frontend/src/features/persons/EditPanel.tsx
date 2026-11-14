import {
	useMutation,
	useQueryClient,
	useSuspenseQuery,
} from "@tanstack/react-query";
import { useState } from "react";
import { FiX } from "react-icons/fi";
import { personsQueryOptions, updatePerson } from "./api";
import { membershipLabels, membershipTypes } from "./membership";
import { PersonPicker } from "./PersonPicker";
import { payerCandidates, payingFor, personLabel } from "./personFilter";
import type { Person } from "./types";

const textFields: {
	key: keyof Person;
	label: string;
	type?: string;
	multiline?: boolean;
}[] = [
	{ key: "firstName", label: "Vorname" },
	{ key: "lastName", label: "Nachname" },
	{ key: "dateOfBirth", label: "Geburtstag", type: "date" },
	{ key: "joinedOn", label: "Eintritt", type: "date" },
	{ key: "leftOn", label: "Austritt", type: "date" },
	{ key: "mobilePhone", label: "Mobilfunknummer", type: "tel" },
	{ key: "landlinePhone", label: "Festnetznummer", type: "tel" },
	{ key: "email", label: "E-Mail", type: "email" },
	{ key: "street", label: "Strasse" },
	{ key: "number", label: "Hausnummer" },
	{ key: "postalCode", label: "PLZ" },
	{ key: "city", label: "Ort" },
	{ key: "country", label: "Land" },
	{ key: "notes", label: "Notizen", multiline: true },
];

export function EditPanel({
	person,
	onClose,
}: {
	person: Person;
	onClose: () => void;
}) {
	const queryClient = useQueryClient();
	const { data: persons } = useSuspenseQuery(personsQueryOptions);
	const paidFor = payingFor(persons, person);
	const [draft, setDraft] = useState(person);
	const [error, setError] = useState<string | null>(null);
	const [saved, setSaved] = useState(false);
	const update = useMutation({
		mutationFn: updatePerson,
		onSuccess: (updated) => {
			setDraft(updated);
			setSaved(true);
			return queryClient.invalidateQueries({
				queryKey: personsQueryOptions.queryKey,
			});
		},
		onError: (err) => setError(err.message),
	});
	const busy = update.isPending;

	function submit(e: React.FormEvent) {
		e.preventDefault();
		setError(null);
		setSaved(false);
		// Empty inputs are stored as null, matching the importer.
		const payload = Object.fromEntries(
			Object.entries(draft).map(([k, v]) => [k, v === "" ? null : v]),
		) as Person;
		update.mutate(payload);
	}

	function renderField(f: (typeof textFields)[number]) {
		const common = {
			id: `person-${f.key}`,
			value: (draft[f.key] as string | null) ?? "",
			required: f.key === "firstName" || f.key === "lastName",
			onChange: (
				e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>,
			) => {
				setDraft({ ...draft, [f.key]: e.target.value });
				setSaved(false);
			},
			className:
				"mt-1 block w-full rounded border border-neutral-300 px-2 py-1 text-sm",
		};
		return (
			<div key={f.key} className={f.multiline ? "col-span-2" : undefined}>
				<label htmlFor={common.id} className="text-sm font-medium">
					{f.label}
				</label>
				{f.multiline ? (
					<textarea rows={4} {...common} />
				) : (
					<input type={f.type ?? "text"} {...common} />
				)}
			</div>
		);
	}

	return (
		<aside className="w-[50%] shrink-0 rounded border border-neutral-200 p-4">
			<form onSubmit={submit}>
				<div className="flex items-center justify-between">
					<h2 className="text-xl font-bold">Person bearbeiten</h2>
					<button
						type="button"
						onClick={onClose}
						aria-label="Abbrechen"
						className="p-1"
					>
						<FiX />
					</button>
				</div>
				<div className="mt-3 grid grid-cols-2 gap-x-4 gap-y-3">
					{textFields.slice(0, 2).map(renderField)}
					<div>
						<label htmlFor="person-membership" className="text-sm font-medium">
							Mitgliedschaft
						</label>
						<select
							id="person-membership"
							value={draft.membership}
							onChange={(e) => {
								setDraft({
									...draft,
									membership: e.target.value as Person["membership"],
								});
								setSaved(false);
							}}
							className="mt-1 block w-full rounded border border-neutral-300 px-2 py-1 text-sm"
						>
							{membershipTypes.map((type) => (
								<option key={type} value={type}>
									{membershipLabels[type]}
								</option>
							))}
						</select>
					</div>
					{textFields.slice(2).flatMap((f) =>
						f.key === "email"
							? [
									renderField(f),
									<label
										key="main-payer"
										className="flex items-end gap-2 pb-1 text-sm font-medium"
									>
										<input
											type="checkbox"
											checked={draft.isMainPayer}
											onChange={(e) => {
												setDraft({ ...draft, isMainPayer: e.target.checked });
												setSaved(false);
											}}
										/>
										Hauptzahler
									</label>,
								]
							: [renderField(f)],
					)}
				</div>
				<div className="mt-3">
					<label htmlFor="person-payer" className="text-sm font-medium">
						Zahler
					</label>
					<PersonPicker
						value={paidFor.length > 0 ? null : draft.payerId}
						readOnly={paidFor.length > 0}
						includeFormer
						accept={(p) => payerCandidates([p], person).length > 0}
						onChange={(id) => {
							setDraft({ ...draft, payerId: id });
							setSaved(false);
						}}
						className="mt-1 block w-full rounded border border-neutral-300 px-2 py-1 text-sm"
					/>
				</div>
				{paidFor.length > 0 && (
					<p className="mt-3 text-sm">
						Zahler für: {paidFor.map(personLabel).join(", ")}
					</p>
				)}
				{error && <p className="mt-3 text-sm text-red-600">{error}</p>}
				{saved && <p className="mt-3 text-sm text-green-700">Gespeichert.</p>}
				<div className="mt-4 flex justify-end gap-2">
					<button
						type="button"
						onClick={onClose}
						className="rounded border border-slate-300 hover:bg-sky-100 px-4 py-2"
					>
						Abbrechen
					</button>
					<button
						type="submit"
						disabled={busy}
						className="rounded bg-sky-600 px-4 py-2 text-white enabled:hover:bg-sky-700 disabled:opacity-40"
					>
						{busy ? "Speichere…" : "Speichern"}
					</button>
				</div>
			</form>
		</aside>
	);
}

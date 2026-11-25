import {
	useMutation,
	useQueryClient,
	useSuspenseQuery,
} from "@tanstack/react-query";
import { useEffect, useRef, useState } from "react";
import { FiX } from "react-icons/fi";
import { deletePerson, personsQueryOptions, updatePerson } from "./api";
import { membershipLabels, membershipTypes } from "./membership";
import { PersonPicker } from "./PersonPicker";
import {
	canBePayerOf,
	canHavePayer,
	personLabel,
	personsPaidBy,
} from "./personFilter";
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
	{ key: "mobilePhone", label: "Mobile", type: "tel" },
	{ key: "landlinePhone", label: "Festnetz", type: "tel" },
	{ key: "email", label: "E-Mail", type: "email" },
	{ key: "street", label: "Strasse" },
	{ key: "number", label: "Hausnummer" },
	{ key: "postalCode", label: "PLZ" },
	{ key: "city", label: "Ort" },
	{ key: "country", label: "Land" },
	{ key: "notes", label: "Notizen", multiline: true },
];

const REGULAR = "regular";
const MAIN_PAYER = "main-payer";
const payerChoices = [
	{ key: REGULAR, label: "Regulär" },
	{
		key: MAIN_PAYER,
		label: "Hauptzahler (Rechnungsempfänger für diese Adresse)",
	},
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
	const paidPersons = personsPaidBy(persons, person);
	// A Person who is someone's Payer cannot get a Payer (nor be Main Payer): the picker is locked.
	const payerLocked = !canHavePayer(persons, person);
	const [draft, setDraft] = useState(person);
	const [baseline, setBaseline] = useState(person);
	const dirty = JSON.stringify(draft) !== JSON.stringify(baseline);
	const panelRef = useRef<HTMLElement>(null);
	// Clicking outside closes the panel, unless that would discard unsaved changes.
	useEffect(() => {
		if (dirty) return;
		const attachedAt = performance.now();
		function onDocumentClick(e: MouseEvent) {
			// The click that opened the panel is still bubbling when this listener is attached.
			if (e.timeStamp < attachedAt) return;
			// composedPath is fixed at dispatch time, so it still works if the target was removed since.
			if (panelRef.current && !e.composedPath().includes(panelRef.current))
				onClose();
		}
		document.addEventListener("click", onDocumentClick);
		return () => document.removeEventListener("click", onDocumentClick);
	}, [dirty, onClose]);
	// A chosen Payer may have gained a Payer of their own since: then they are no longer valid.
	const chosenPayer = persons.find((p) => p.id === draft.payerId);
	const payerStale =
		chosenPayer !== undefined && !canBePayerOf(chosenPayer, person);
	const [error, setError] = useState<string | null>(null);
	const [saved, setSaved] = useState(false);
	const update = useMutation({
		mutationFn: updatePerson,
		onSuccess: (updated) => {
			setDraft(updated);
			setBaseline(updated);
			setSaved(true);
			return queryClient.invalidateQueries({
				queryKey: personsQueryOptions.queryKey,
			});
		},
		onError: (err) => setError(err.message),
	});
	const busy = update.isPending;
	const del = useMutation({
		mutationFn: () => deletePerson(person.id),
		onSuccess: () => {
			return queryClient
				.invalidateQueries({ queryKey: personsQueryOptions.queryKey })
				.then(onClose);
		},
		onError: (err) => setError(err.message),
	});
	const deleting = del.isPending;

	function onDelete() {
		const msg = `Person "${personLabel(person)}" löschen? Das kann nicht rückgängig gemacht werden.`;
		if (!window.confirm(msg)) return;
		setError(null);
		del.mutate();
	}

	function submit(e: React.FormEvent) {
		e.preventDefault();
		setError(null);
		setSaved(false);
		// Empty inputs are stored as null, matching the importer.
		const payload = Object.fromEntries(
			Object.entries(draft).map(([k, v]) => [k, v === "" ? null : v]),
		) as Person;
		if (payerStale) payload.payerId = null;
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
		<aside
			ref={panelRef}
			className="w-[50%] shrink-0 rounded border border-neutral-200 p-4"
		>
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
							{draft.membership === "unknown" && (
								<option value="unknown">{membershipLabels.unknown}</option>
							)}
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
									<div key="payer">
										<label
											htmlFor="person-payer"
											className="text-sm font-medium"
										>
											Zahler
										</label>
										<PersonPicker
											value={payerStale ? null : draft.payerId}
											choices={payerChoices}
											choice={draft.isMainPayer ? MAIN_PAYER : REGULAR}
											onChoice={(key) => {
												setDraft({
													...draft,
													payerId: null,
													isMainPayer: key === MAIN_PAYER,
												});
												setSaved(false);
											}}
											includeFormer
											readOnly={payerLocked}
											selectable={(p) => canBePayerOf(p, person)}
											onChange={(id) => {
												// A named Payer replaces Main Payer; clearing the field falls back to Regulär.
												setDraft({ ...draft, payerId: id, isMainPayer: false });
												setSaved(false);
											}}
											className={`mt-1 block w-full rounded border border-neutral-300 px-2 py-1 text-sm ${payerLocked ? "bg-neutral-100 text-neutral-500" : ""}`}
										/>
										{payerStale && (
											<p className="mt-1 text-sm text-red-600">
												Der bisherige Zahler hat inzwischen selbst einen Zahler
												und wird beim Speichern entfernt.
											</p>
										)}
										{paidPersons.length > 0 && (
											<p className="mt-1 text-sm">
												Zahler für: {paidPersons.map(personLabel).join(", ")}
											</p>
										)}
									</div>,
								]
							: f.key === "country"
								? [
										renderField(f),
										<label
											key="club-news"
											className="flex items-end gap-2 pb-1 text-sm font-medium"
										>
											<input
												type="checkbox"
												checked={draft.receivesClubNews}
												onChange={(e) => {
													setDraft({
														...draft,
														receivesClubNews: e.target.checked,
													});
													setSaved(false);
												}}
											/>
											Club-News
										</label>,
									]
								: [renderField(f)],
					)}
				</div>
				{error && <p className="mt-3 text-sm text-red-600">{error}</p>}
				{saved && <p className="mt-3 text-sm text-green-700">Gespeichert.</p>}
				<div className="mt-4 flex justify-end gap-2">
					<button
						type="button"
						onClick={onDelete}
						disabled={deleting}
						title="Person löschen"
						className="mr-auto rounded border border-red-300 px-4 py-2 text-red-700 enabled:hover:bg-red-50 disabled:opacity-40"
					>
						Löschen
					</button>
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

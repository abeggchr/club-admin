import {
	useMutation,
	useQueryClient,
	useSuspenseQuery,
} from "@tanstack/react-query";
import { useState } from "react";
import { personsQueryOptions } from "../persons/api";
import { PersonPicker } from "../persons/PersonPicker";
import { personLabel } from "../persons/personFilter";
import {
	deleteMembershipDiscount,
	membershipDiscountsQueryOptions,
	saveMembershipDiscount,
} from "./api";

/** Ermässigungen: one discount per Person, saved immediately and independent of the run. */
export function DiscountsSection({ disabled }: { disabled: boolean }) {
	const queryClient = useQueryClient();
	const { data: discounts } = useSuspenseQuery(membershipDiscountsQueryOptions);
	const { data: persons } = useSuspenseQuery(personsQueryOptions);
	const [personId, setPersonId] = useState<number | null>(null);
	const [percent, setPercent] = useState("");
	const [text, setText] = useState("");

	const refresh = () =>
		queryClient.invalidateQueries({
			queryKey: membershipDiscountsQueryOptions.queryKey,
		});
	const save = useMutation({
		mutationFn: saveMembershipDiscount,
		onSuccess: () => {
			setPersonId(null);
			setPercent("");
			setText("");
			return refresh();
		},
	});
	const remove = useMutation({
		mutationFn: deleteMembershipDiscount,
		onSuccess: refresh,
	});

	const nameOf = (id: number) => {
		const p = persons.find((x) => x.id === id);
		return p ? personLabel(p) : `Person ${id}`;
	};

	function loadDiscountIntoForm(personId: number) {
		const d = discounts.find((x) => x.personId === personId);
		setPersonId(personId);
		setPercent(d ? String(d.percent) : "");
		setText(d?.text ?? "");
	}

	function submit() {
		if (personId === null) return;
		save.mutate({ personId, percent: Number(percent), text });
	}

	return (
		<fieldset
			className="mt-4"
			disabled={disabled}
			onKeyDown={(e) => {
				// Enter saves the discount instead of submitting the run form around it.
				if (e.key === "Enter" && e.target instanceof HTMLInputElement) {
					e.preventDefault();
					submit();
				}
			}}
		>
			<legend className="text-sm font-medium">Ermässigungen</legend>
			{discounts.length > 0 && (
				<ul className="mt-2 text-sm">
					{discounts.map((d) => (
						<li key={d.personId} className="flex items-center gap-2 py-0.5">
							<span className="flex-1">
								{nameOf(d.personId)} – {d.percent} %: {d.text}
							</span>
							<button
								type="button"
								onClick={() => loadDiscountIntoForm(d.personId)}
								className="text-sky-700 underline"
							>
								Ändern
							</button>
							<button
								type="button"
								onClick={() => remove.mutate(d.personId)}
								className="text-red-600 underline"
							>
								Entfernen
							</button>
						</li>
					))}
				</ul>
			)}
			<div className="mt-2 flex items-start gap-2 text-sm">
				<PersonPicker
					value={personId}
					onChange={(id) => {
						if (id !== null) loadDiscountIntoForm(id);
						else setPersonId(null);
					}}
					className="w-40 rounded border border-slate-300 p-1"
				/>
				<input
					type="number"
					min={1}
					max={100}
					aria-label="Prozent"
					placeholder="%"
					value={percent}
					onChange={(e) => setPercent(e.target.value)}
					className="w-16 rounded border border-slate-300 p-1"
				/>
				<input
					type="text"
					aria-label="Text"
					placeholder="Text"
					value={text}
					onChange={(e) => setText(e.target.value)}
					className="min-w-0 flex-1 rounded border border-slate-300 p-1"
				/>
				<button
					type="button"
					onClick={submit}
					disabled={personId === null || save.isPending}
					className="rounded border border-neutral-300 px-3 py-1 disabled:opacity-40"
				>
					Speichern
				</button>
			</div>
			{(save.error || remove.error) && (
				<p className="mt-2 text-sm text-red-600">
					{(save.error ?? remove.error)?.message}
				</p>
			)}
		</fieldset>
	);
}

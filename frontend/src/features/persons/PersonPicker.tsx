import { useSuspenseQuery } from "@tanstack/react-query";
import { useId, useState } from "react";
import { personsQueryOptions } from "./api";
import { filterPersons, personLabel } from "./personFilter";
import type { Person } from "./types";

export type PickerChoice = { key: string; label: string };

type Option =
	| { kind: "choice"; choice: PickerChoice }
	| { kind: "person"; person: Person };

const optionId = (o: Option) =>
	o.kind === "choice" ? `choice-${o.choice.key}` : String(o.person.id);

type Props = {
	value: number | null;
	onChange: (id: number | null) => void;
	/** When true the picker shows the selected Person but does not open or report changes. */
	readOnly?: boolean;
	className?: string;
	/** Also offer Persons who have left (marked "(ausgetreten)"). */
	includeFormer?: boolean;
	/** Only Persons for which this returns true are offered (the current value is always kept). */
	selectable?: (p: Person) => boolean;
	/** Fixed entries offered above the Persons (e.g. "Regulär"). */
	choices?: PickerChoice[];
	/** Key of the active choice; shown when no Person is selected. */
	choice?: string | null;
	onChoice?: (key: string) => void;
};

/** Person autocomplete: type part of a name to filter. Persons who have left are not offered. */
export function PersonPicker({
	value,
	onChange,
	readOnly = false,
	className,
	includeFormer,
	selectable,
	choices = [],
	choice = null,
	onChoice,
}: Props) {
	const { data: persons } = useSuspenseQuery(personsQueryOptions);
	const listId = useId();
	const [query, setQuery] = useState<string | null>(null); // null = not editing
	const [open, setOpen] = useState(false);
	const [highlight, setHighlight] = useState(0);

	const selected = persons.find((p) => p.id === value) ?? null;
	const activeChoice = choices.find((c) => c.key === choice) ?? null;
	const text =
		query ?? (selected ? personLabel(selected) : (activeChoice?.label ?? ""));
	const personOptions = filterPersons(
		selectable
			? persons.filter((p) => p.id === value || selectable(p))
			: persons,
		query ?? "",
		value,
		{ includeFormer },
	);
	const needle = (query ?? "").trim().toLowerCase();
	const options: Option[] = [
		...choices
			.filter((c) => c.label.toLowerCase().includes(needle))
			.map((c): Option => ({ kind: "choice", choice: c })),
		...personOptions.map((p): Option => ({ kind: "person", person: p })),
	];

	const close = () => {
		setOpen(false);
		setQuery(null);
	};

	const choose = (o: Option) => {
		if (o.kind === "choice") onChoice?.(o.choice.key);
		else onChange(o.person.id);
		close();
	};

	const onKeyDown = (e: React.KeyboardEvent) => {
		if (readOnly) return;
		if (e.key === "ArrowDown") {
			e.preventDefault();
			setOpen(true);
			setHighlight((h) => Math.min(h + 1, options.length - 1));
		} else if (e.key === "ArrowUp") {
			e.preventDefault();
			setHighlight((h) => Math.max(h - 1, 0));
		} else if (e.key === "Enter" && open) {
			e.preventDefault();
			const o = options[highlight];
			if (o) choose(o);
		} else if (e.key === "Escape" && open) {
			e.stopPropagation();
			close();
		}
	};

	return (
		<div className="relative flex flex-col">
			<input
				type="text"
				role="combobox"
				aria-expanded={open}
				aria-controls={listId}
				aria-autocomplete="list"
				aria-activedescendant={
					open && options[highlight]
						? `${listId}-${optionId(options[highlight])}`
						: undefined
				}
				autoComplete="off"
				readOnly={readOnly}
				placeholder={readOnly ? undefined : "Person wählen…"}
				className={className}
				value={text}
				onFocus={(e) => {
					if (readOnly) return;
					setOpen(true);
					e.target.select();
				}}
				onChange={(e) => {
					if (readOnly) return;
					setQuery(e.target.value);
					setHighlight(0);
					setOpen(true);
					if (e.target.value.trim() === "") onChange(null);
				}}
				onBlur={close}
				onKeyDown={onKeyDown}
			/>
			{open && (
				<div
					id={listId}
					role="listbox"
					className="absolute left-0 right-0 top-full z-10 mt-1 max-h-64 overflow-auto rounded border border-slate-300 bg-white shadow"
				>
					{options.length === 0 && (
						<div className="p-2 text-slate-500">Keine Person gefunden</div>
					)}
					{options.map((o, i) => (
						<div
							key={optionId(o)}
							id={`${listId}-${optionId(o)}`}
							role="option"
							tabIndex={-1}
							aria-selected={
								o.kind === "choice"
									? value === null && o.choice.key === choice
									: o.person.id === value
							}
							// mousedown (not click) so it fires before the input's blur closes the list
							onMouseDown={(e) => {
								e.preventDefault();
								choose(o);
							}}
							onMouseEnter={() => setHighlight(i)}
							className={`cursor-pointer px-2 py-1 ${i === highlight ? "bg-sky-100" : ""}`}
						>
							{o.kind === "choice" ? (
								o.choice.label
							) : (
								<>
									{personLabel(o.person)}
									{o.person.leftOn !== null ? " (ausgetreten)" : ""}
								</>
							)}
						</div>
					))}
				</div>
			)}
		</div>
	);
}

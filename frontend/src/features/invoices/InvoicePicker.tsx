import { useQueryClient, useSuspenseQuery } from "@tanstack/react-query";
import { useId, useState } from "react";
import { formatChf } from "../accounting/amount";
import {
	type InvoiceDetail,
	type InvoiceListItem,
	invoiceDetailQueryOptions,
	invoicesQueryOptions,
} from "./api";

export function invoiceLabel(i: InvoiceListItem): string {
	return `${i.number} ${i.person.name} ${formatChf(i.total)}`;
}

/** Open Invoices (status Created) matching the typed number, name or amount, oldest number first. */
export function filterOpenInvoices(
	invoices: InvoiceListItem[],
	query: string,
): InvoiceListItem[] {
	const q = query.trim().toLowerCase();
	return invoices
		.filter((i) => i.status === "Created")
		.filter(
			(i) =>
				q === "" ||
				i.number.includes(q) ||
				i.person.name.toLowerCase().includes(q) ||
				formatChf(i.total).includes(q),
		)
		.sort((a, b) => a.id - b.id);
}

type Props = {
	onSelect: (invoice: InvoiceDetail) => void;
	id?: string;
	className?: string;
};

/**
 * Open-Invoice autocomplete: type an Invoice Number, a name or an amount; each option shows number, name and
 * total. Choosing one loads its detail (with the Lines) and reports it; the field then clears.
 */
export function InvoicePicker({ onSelect, id, className }: Props) {
	const { data: invoices } = useSuspenseQuery(invoicesQueryOptions);
	const queryClient = useQueryClient();
	const listId = useId();
	const [query, setQuery] = useState("");
	const [open, setOpen] = useState(false);
	const [highlight, setHighlight] = useState(0);
	const [error, setError] = useState<string>();
	const options = filterOpenInvoices(invoices, query);

	const close = () => {
		setOpen(false);
		setQuery("");
	};

	async function choose(i: InvoiceListItem) {
		close();
		setError(undefined);
		try {
			onSelect(
				await queryClient.fetchQuery({
					...invoiceDetailQueryOptions(i.id),
					staleTime: 0,
				}),
			);
		} catch (err) {
			setError(
				err instanceof Error ? err.message : "Rechnung laden fehlgeschlagen.",
			);
		}
	}

	const onKeyDown = (e: React.KeyboardEvent) => {
		if (e.key === "ArrowDown") {
			e.preventDefault();
			setOpen(true);
			setHighlight((h) => Math.min(h + 1, options.length - 1));
		} else if (e.key === "ArrowUp") {
			e.preventDefault();
			setHighlight((h) => Math.max(h - 1, 0));
		} else if (e.key === "Enter" && open) {
			e.preventDefault();
			const i = options[highlight];
			if (i) void choose(i);
		} else if (e.key === "Escape" && open) {
			e.stopPropagation();
			close();
		}
	};

	return (
		<div className="relative flex flex-col">
			<input
				id={id}
				type="text"
				role="combobox"
				aria-expanded={open}
				aria-controls={listId}
				aria-autocomplete="list"
				aria-activedescendant={
					open && options[highlight]
						? `${listId}-${options[highlight].id}`
						: undefined
				}
				autoComplete="off"
				placeholder="Offene Rechnung wählen…"
				className={className}
				value={query}
				onFocus={() => setOpen(true)}
				onChange={(e) => {
					setQuery(e.target.value);
					setHighlight(0);
					setOpen(true);
				}}
				onBlur={close}
				onKeyDown={onKeyDown}
			/>
			{error && <span className="text-red-600">{error}</span>}
			{open && (
				<div
					id={listId}
					role="listbox"
					className="absolute left-0 right-0 top-full z-10 mt-1 max-h-64 overflow-auto rounded border border-slate-300 bg-white shadow"
				>
					{options.length === 0 && (
						<div className="p-2 text-slate-500">Keine offene Rechnung</div>
					)}
					{options.map((i, idx) => (
						<div
							key={i.id}
							id={`${listId}-${i.id}`}
							role="option"
							tabIndex={-1}
							aria-selected={idx === highlight}
							// mousedown (not click) so it fires before the input's blur closes the list
							onMouseDown={(e) => {
								e.preventDefault();
								void choose(i);
							}}
							onMouseEnter={() => setHighlight(idx)}
							className={`flex cursor-pointer items-center justify-between gap-2 px-2 py-1 ${
								idx === highlight ? "bg-sky-100" : ""
							}`}
						>
							<span>
								{i.number} {i.person.name}
							</span>
							<span className="tabular-nums">{formatChf(i.total)}</span>
						</div>
					))}
				</div>
			)}
		</div>
	);
}

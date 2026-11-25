import { useSuspenseQuery } from "@tanstack/react-query";
import { useRef, useState } from "react";
import { MdAdd } from "react-icons/md";
import { formatChf } from "../accounting/amount";
import { settingsQueryOptions } from "../accounting/api";
import { textTemplatesQueryOptions } from "../settings/textTemplateApi";
import { AnnualFeesDialog } from "./AnnualFeesDialog";
import { formatDate, invoicesQueryOptions, statusLabel } from "./api";
import { BulkSendDialog } from "./BulkSendDialog";
import { CreatePanel } from "./CreatePanel";
import { DetailPanel } from "./DetailPanel";
import { type InvoiceFilterKey, invoiceFilters } from "./invoiceFilter";

/** The Invoices area: table (newest first) | create panel (only while open). */
export function InvoicesView() {
	const { data: invoices } = useSuspenseQuery(invoicesQueryOptions);
	const { data: templates } = useSuspenseQuery(textTemplatesQueryOptions);
	const { data: settings } = useSuspenseQuery(settingsQueryOptions);
	const annualFeesDialog = useRef<HTMLDialogElement>(null);
	const bulkSendDialog = useRef<HTMLDialogElement>(null);
	const [creating, setCreating] = useState(false);
	const [selectedId, setSelectedId] = useState<number | null>(null);
	const [filterKey, setFilterKey] = useState<InvoiceFilterKey>("open");
	const noTemplates = templates.length === 0;
	const noChannel = !settings?.emailConfigured && !settings?.ePostConfigured;
	const rows = invoices
		.filter(invoiceFilters[filterKey].filter)
		.sort((a, b) => b.id - a.id);

	return (
		<>
			<div className="mt-4 flex items-center gap-2">
				<button
					type="button"
					onClick={() => {
						setSelectedId(null);
						setCreating(true);
					}}
					disabled={noTemplates || creating}
					aria-label="Neue Rechnung"
					title="Neue Rechnung"
					className="rounded bg-sky-600 p-2 text-white enabled:hover:bg-sky-700 disabled:opacity-40"
				>
					<MdAdd className="size-5" />
				</button>
				<button
					type="button"
					onClick={() => annualFeesDialog.current?.showModal()}
					disabled={noTemplates}
					title="Jahresrechnungen erstellen"
					className="rounded border border-sky-600 px-3 py-2 text-sm text-sky-700 enabled:hover:bg-sky-50 disabled:opacity-40"
				>
					Jahresbeiträge
				</button>
				<button
					type="button"
					onClick={() => bulkSendDialog.current?.showModal()}
					disabled={noChannel}
					title={
						noChannel
							? "Weder E-Mail noch ePost ist eingerichtet."
							: "Alle nicht versendeten Rechnungen versenden"
					}
					className="rounded border border-sky-600 px-3 py-2 text-sm text-sky-700 enabled:hover:bg-sky-50 disabled:opacity-40"
				>
					Versenden
				</button>
				<fieldset className="ml-4 inline-flex">
					<legend className="sr-only">Ansicht</legend>
					{Object.entries(invoiceFilters).map(([key, f]) => (
						<button
							key={key}
							type="button"
							aria-pressed={key === filterKey}
							onClick={() => setFilterKey(key as InvoiceFilterKey)}
							className={`-ml-px border border-neutral-300 px-3 py-2 text-sm first:ml-0 first:rounded-l last:rounded-r ${
								key === filterKey
									? "bg-neutral-800 text-white"
									: "hover:bg-neutral-50"
							}`}
						>
							{f.label}
						</button>
					))}
				</fieldset>
			</div>

			<div className="mt-4 flex items-start gap-4">
				<div className="min-w-0 flex-1 overflow-x-auto rounded border border-slate-200">
					<table className="w-full text-left text-sm">
						<thead className="border-b border-slate-200 bg-slate-50">
							<tr>
								<th className="px-3 py-2 font-medium">Nr.</th>
								<th className="px-3 py-2 font-medium">Datum</th>
								<th className="px-3 py-2 font-medium">Person</th>
								<th className="px-3 py-2 text-right font-medium">Total</th>
								<th className="px-3 py-2 font-medium">Status</th>
							</tr>
						</thead>
						<tbody>
							{rows.length === 0 ? (
								<tr>
									<td
										colSpan={5}
										className="px-3 py-8 text-center text-slate-500"
									>
										Noch keine Rechnungen.
									</td>
								</tr>
							) : (
								rows.map((r) => (
									<tr
										key={r.id}
										onClick={() => {
											setCreating(false);
											setSelectedId(r.id);
										}}
										className={`cursor-pointer border-b border-slate-100 last:border-0 hover:bg-sky-50 ${r.id === selectedId ? "bg-sky-100" : ""}`}
									>
										<td className="px-3 py-2 tabular-nums">{r.number}</td>
										<td className="px-3 py-2">{formatDate(r.date)}</td>
										<td className="px-3 py-2">{r.person.name}</td>
										<td className="px-3 py-2 text-right tabular-nums">
											{formatChf(r.total)}
										</td>
										<td className="px-3 py-2">{statusLabel(r.status)}</td>
									</tr>
								))
							)}
						</tbody>
					</table>
				</div>
				{creating && (
					<aside className="flex-1 shrink-0 rounded border border-slate-200 p-4">
						<CreatePanel onDone={() => setCreating(false)} />
					</aside>
				)}
				{!creating && selectedId !== null && (
					<aside className="flex-1 shrink-0 rounded border border-slate-200 p-4">
						<DetailPanel
							key={selectedId}
							id={selectedId}
							onClose={() => setSelectedId(null)}
						/>
					</aside>
				)}
			</div>
			<AnnualFeesDialog dialogRef={annualFeesDialog} />
			<BulkSendDialog dialogRef={bulkSendDialog} />
		</>
	);
}

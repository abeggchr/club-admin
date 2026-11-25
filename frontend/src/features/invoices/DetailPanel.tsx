import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";
import { MdClose } from "react-icons/md";
import { accountLabel } from "../accounting/AccountPicker";
import { formatChf } from "../accounting/amount";
import { accountsQueryOptions } from "../accounting/api";
import {
	cancelInvoice,
	formatDate,
	invalidateInvoices,
	invoiceDetailQueryOptions,
	invoicePdfUrl,
	statusLabel,
} from "./api";
import { formatDateTime } from "./invoiceDetail";

/** Detail of an Invoice. Nothing is editable or deletable; an open Invoice can be cancelled (storniert). */
export function DetailPanel({
	id,
	onClose,
}: {
	id: number;
	onClose: () => void;
}) {
	const {
		data: invoice,
		isPending,
		isError,
	} = useQuery(invoiceDetailQueryOptions(id));
	const { data: accounts = [] } = useQuery(accountsQueryOptions);
	const queryClient = useQueryClient();
	const cancel = useMutation({
		mutationFn: () => cancelInvoice(id),
		onSettled: () => invalidateInvoices(queryClient),
	});

	const accountOf = (accountNumber: number) => {
		const account = accounts.find((a) => a.number === accountNumber);
		return account ? accountLabel(account) : accountNumber;
	};

	return (
		<div className="flex flex-col gap-3">
			<div className="flex items-center justify-between">
				<h3 className="font-medium">
					{invoice ? `Rechnung Nr. ${invoice.number}` : "Rechnung"}
				</h3>
				<button
					type="button"
					onClick={onClose}
					aria-label="Schliessen"
					title="Schliessen"
					className="rounded p-0.5 hover:bg-sky-100"
				>
					<MdClose className="size-5" />
				</button>
			</div>
			{isPending && <p>Loading…</p>}
			{isError && (
				<p className="text-red-600">Rechnung konnte nicht geladen werden.</p>
			)}
			{invoice && (
				<>
					<p className="text-sm">
						<span className="text-slate-500">Datum:</span>{" "}
						{formatDate(invoice.date)}
					</p>

					{invoice.reference !== "" && (
						<p className="text-sm">
							<span className="text-slate-500">Referenz:</span>{" "}
							{invoice.reference}
						</p>
					)}

					<div className="text-sm">
						<span className="text-slate-500">Empfänger: </span>
						<Link
							to="/persons"
							search={{ person: invoice.person.id }}
							className="text-sky-700 underline hover:text-sky-900"
						>
							{invoice.person.name}
						</Link>
					</div>

					<span className="text-sm text-slate-500">Positionen: </span>
					<table className="w-full text-left text-sm">
						<thead className="border-b border-slate-200">
							<tr>
								<th className="py-1 font-medium">Konto</th>
								<th className="py-1 font-medium">Text</th>
								<th className="py-1 text-right font-medium">Betrag</th>
							</tr>
						</thead>
						<tbody>
							{invoice.lines.map((l, i) => (
								// biome-ignore lint/suspicious/noArrayIndexKey: read-only lines have no id
								<tr key={i} className="border-b border-slate-100">
									<td className="py-1">{accountOf(l.accountNumber)}</td>
									<td className="py-1">{l.text}</td>
									<td className="py-1 text-right tabular-nums">
										{formatChf(l.amount)}
									</td>
								</tr>
							))}
							<tr className="font-medium">
								<td className="py-1">Total</td>
								<td className="py-1 text-right tabular-nums" colSpan={2}>
									{formatChf(invoice.total)}
								</td>
							</tr>
						</tbody>
					</table>

					<div className="text-sm">
						<p className="text-slate-500">Status</p>
						<ul>
							{invoice.statusHistory.map((s) => (
								<li key={`${s.status}-${s.timestamp}`}>
									{statusLabel(s.status)} – {formatDateTime(s.timestamp)}
									{s.remark && ` – ${s.remark}`}
								</li>
							))}
						</ul>
					</div>

					{invoice.paidBy && (
						<p className="text-sm">Bezahlt mit Bewegung Nr. {invoice.paidBy}</p>
					)}

					<div className="flex items-center gap-2">
						<a
							href={invoicePdfUrl(invoice.id)}
							target="_blank"
							rel="noreferrer"
							className="rounded bg-sky-600 px-4 py-2 text-white hover:bg-sky-700"
						>
							PDF öffnen
						</a>

						{invoice.status === "Created" && (
							<button
								type="button"
								disabled={cancel.isPending}
								onClick={() => {
									if (
										window.confirm(
											`Rechnung Nr. ${invoice.number} stornieren? Das kann nicht rückgängig gemacht werden.`,
										)
									)
										cancel.mutate();
								}}
								className="rounded border border-red-600 px-4 py-2 text-red-700 enabled:hover:bg-red-50 disabled:opacity-40"
							>
								Stornieren
							</button>
						)}
					</div>
					{cancel.isError && (
						<p className="text-sm text-red-600">{cancel.error.message}</p>
					)}
				</>
			)}
		</div>
	);
}

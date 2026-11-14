import { useQuery } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";
import { MdClose } from "react-icons/md";
import { formatChf } from "../accounting/amount";
import { accountsQueryOptions } from "../accounting/api";
import { accountLabel } from "../accounting/KontoPicker";
import {
	formatDate,
	invoiceDetailQueryOptions,
	invoicePdfUrl,
	statusLabel,
} from "./api";
import { addressLines, formatDateTime } from "./invoiceDetail";

/** Read-only detail of a Rechnung. Nothing here is editable or deletable. */
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
					<p className="text-sm">Rechnungsdatum: {formatDate(invoice.date)}</p>

					{invoice.reference !== "" && (
						<p className="text-sm">Gläubigerreferenz: {invoice.reference}</p>
					)}

					<div className="text-sm">
						<p className="text-slate-500">Person</p>
						<Link
							to="/persons"
							search={{ person: invoice.person.id }}
							className="text-sky-700 underline hover:text-sky-900"
						>
							{invoice.person.name}
						</Link>
						{addressLines(invoice.person).map((line) => (
							<p key={line}>{line}</p>
						))}
					</div>

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
								</li>
							))}
						</ul>
					</div>

					{invoice.paidBy && (
						<p className="text-sm">Bezahlt mit Bewegung Nr. {invoice.paidBy}</p>
					)}

					<a
						href={invoicePdfUrl(invoice.id)}
						target="_blank"
						rel="noreferrer"
						className="self-start rounded bg-sky-600 px-4 py-2 text-white hover:bg-sky-700"
					>
						PDF öffnen
					</a>
				</>
			)}
		</div>
	);
}

import { useSuspenseQuery } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";
import { formatChf } from "../accounting/amount";
import { formatDate } from "../invoices/api";
import {
	type JubileePerson,
	jubileesQueryOptions,
	sortJubilees,
} from "../jubilees/api";
import {
	invoicesSummaryQueryOptions,
	membershipPie,
	personsSummaryQueryOptions,
} from "./api";
import { PIE_SIZE } from "./pie";

const emptyJubilees = "Keine Jubiläen im laufenden Clubjahr";

function JubileesTable({
	title,
	rows,
	dateLabel,
}: {
	title: string;
	rows: JubileePerson[];
	dateLabel: string;
}) {
	return (
		<section className="mt-8">
			<h3 className="text-lg font-semibold text-sky-900">{title}</h3>
			<div className="mt-2 overflow-x-auto rounded border border-slate-200">
				<table className="w-full text-left text-sm">
					<thead className="border-b border-slate-200 bg-slate-50">
						<tr>
							<th className="px-3 py-2 font-medium">Name</th>
							<th className="px-3 py-2 font-medium">Jahre</th>
							<th className="px-3 py-2 font-medium">{dateLabel}</th>
						</tr>
					</thead>
					<tbody>
						{rows.length === 0 ? (
							<tr>
								<td
									colSpan={3}
									className="px-3 py-8 text-center text-slate-500"
								>
									{emptyJubilees}
								</td>
							</tr>
						) : (
							rows.map((r) => (
								<tr
									key={r.personId}
									className="border-b border-slate-100 last:border-0"
								>
									<td className="px-3 py-2">
										{r.lastName}, {r.firstName}
									</td>
									<td className="px-3 py-2 tabular-nums">{r.years}</td>
									<td className="px-3 py-2">
										{formatDate(r.birthdate ?? r.entryDate ?? "")}
									</td>
								</tr>
							))
						)}
					</tbody>
				</table>
			</div>
		</section>
	);
}

export function MembersTile() {
	const { data: summary } = useSuspenseQuery(personsSummaryQueryOptions);
	const slices = membershipPie(summary);

	return (
		<section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
			<h3 className="text-lg font-semibold text-sky-900">Mitglieder</h3>
			<p className="mt-1 text-4xl font-bold tabular-nums text-sky-900">
				{summary.total}
			</p>
			{summary.total === 0 ? (
				<p className="mt-4 text-slate-500">Keine aktiven Mitglieder</p>
			) : (
				<div className="mt-4 flex flex-wrap items-center gap-6">
					<svg
						viewBox={`0 0 ${PIE_SIZE} ${PIE_SIZE}`}
						className="size-32"
						role="img"
						aria-label="Mitglieder nach Mitgliedschaft"
					>
						{slices.map((s) => (
							<path key={s.membership} d={s.path} fill={s.color} />
						))}
					</svg>
					<ul className="space-y-1 text-sm">
						{slices.map((s) => (
							<li key={s.membership} className="flex items-center gap-2">
								<span
									className="inline-block size-3 rounded-sm"
									style={{ backgroundColor: s.color }}
								/>
								<span className="text-slate-700">{s.label}</span>
								<span className="ml-auto pl-4 tabular-nums">{s.count}</span>
							</li>
						))}
					</ul>
				</div>
			)}
		</section>
	);
}

export function InvoicesTile() {
	const { data: summary } = useSuspenseQuery(invoicesSummaryQueryOptions);

	return (
		<section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
			<h3 className="text-lg font-semibold text-sky-900">Offene Rechnungen</h3>
			<p className="mt-1 text-4xl font-bold tabular-nums text-sky-900">
				{summary.openCount}
			</p>
			<p className="mt-1 text-lg tabular-nums text-slate-700">
				CHF {formatChf(summary.openAmount)}
			</p>
			<Link
				to="/invoices"
				className="mt-4 inline-block text-sm font-medium text-sky-700 hover:underline"
			>
				Zu den Rechnungen →
			</Link>
		</section>
	);
}

/** The Jubiläen section of the dashboard: unchanged tables, loaded in the route's loader. */
export function JubilaeenSection() {
	const { data: jubilees } = useSuspenseQuery(jubileesQueryOptions);

	return (
		<section className="mt-8">
			<h2 className="text-xl font-semibold text-sky-900">
				Jubiläen {jubilees.clubYearStartYear}
			</h2>
			<JubileesTable
				title="Geburtstag"
				rows={sortJubilees(jubilees.anniversaries)}
				dateLabel="Geburtsdatum"
			/>
			<JubileesTable
				title="Jubiläum"
				rows={sortJubilees(jubilees.jubilees)}
				dateLabel="Eintrittsdatum"
			/>
		</section>
	);
}

export function Dashboard() {
	return (
		<div className="px-8 pb-8">
			<h2 className="mt-4 text-xl font-semibold text-sky-900">Übersicht</h2>
			<div className="mt-4 grid gap-4 md:grid-cols-2">
				<MembersTile />
				<InvoicesTile />
			</div>
			<JubilaeenSection />
		</div>
	);
}

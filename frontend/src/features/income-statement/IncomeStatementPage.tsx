import { useQuery } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";
import { formatChf } from "../accounting/amount";
import { formatDe } from "../persons/format";
import { incomeStatementQueryOptions } from "./api";

/**
 * The Income Statement (Erfolgsrechnung) page: the backend computes the whole tree,
 * the frontend only renders it. Title with the period subtitle; "all" view until
 * the simplified view lands.
 */
export function IncomeStatementPage() {
	const { data: report, isPending } = useQuery(incomeStatementQueryOptions);

	if (isPending || !report) {
		return <p className="p-8">Laden…</p>;
	}

	const period = `${formatDe(report.period.from)} – ${formatDe(report.period.to)}`;

	return (
		<div className="mx-auto mt-4 max-w-3xl px-4 pb-8">
			<h2 className="text-xl font-bold text-sky-900">Erfolgsrechnung</h2>
			<p className="text-sm text-sky-700">{period}</p>
			<dl className="mt-4 space-y-6">
				{report.sections.map((section) => (
					<section key={section.label}>
						<h3 className="font-semibold text-sky-900">{section.label}</h3>
						<div className="mt-2 space-y-3">
							{section.groups.map((group) => (
								<div key={group.label}>
									<h4 className="text-sm text-sky-800">
										{group.label} … {formatChf(group.total)}
									</h4>
									<dl className="mt-1 space-y-0.5">
										{group.lines.map((line) => (
											<div
												key={line.label}
												className="flex justify-between text-sm text-sky-900"
											>
												<dt>{line.label}</dt>
												<dd className="tabular-nums">
													{formatChf(line.amount)}
												</dd>
											</div>
										))}
									</dl>
								</div>
							))}
							<div className="mt-2 flex justify-between border-t border-sky-200 pt-1 text-sm font-semibold text-sky-900">
								<span>Gesamt {section.label}</span>
								<span className="tabular-nums">{formatChf(section.total)}</span>
							</div>
						</div>
					</section>
				))}
			</dl>
			<div className="mt-6 border-t-2 border-sky-300 pt-2 text-sm font-bold text-sky-900">
				<div className="flex justify-between">
					<span>Gesamt Ertrag</span>
					<span className="tabular-nums">{formatChf(report.totalRevenue)}</span>
				</div>
				<div className="flex justify-between">
					<span>Gesamt Aufwand</span>
					<span className="tabular-nums">{formatChf(report.totalExpense)}</span>
				</div>
				<div className="mt-1 flex justify-between text-base">
					<span>Gesamtergebnis</span>
					<span className="tabular-nums">
						{formatChf(report.overallResult)}
					</span>
				</div>
			</div>
		</div>
	);
}

/** No Start Date: the backend's German error plus a hint to the Einstellungen. */
function IncomeStatementErrorPage({ error }: { error: unknown }) {
	const message = error instanceof Error ? error.message : String(error);
	const missingStart = message.includes("Startdatum");
	return (
		<div className="mx-auto mt-4 max-w-3xl px-4">
			<h2 className="text-xl font-bold text-sky-900">Erfolgsrechnung</h2>
			<p className="mt-4 text-sm text-red-600">{message}</p>
			{missingStart && (
				<p className="mt-2 text-sm text-sky-800">
					Das Startdatum fehlt. Bitte legen Sie es in den{" "}
					<Link to="/settings" className="underline">
						Einstellungen
					</Link>{" "}
					fest.
				</p>
			)}
		</div>
	);
}

export function IncomeStatementErrorComponent(props: { error: unknown }) {
	return <IncomeStatementErrorPage error={props.error} />;
}

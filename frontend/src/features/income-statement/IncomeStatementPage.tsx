import { useQuery } from "@tanstack/react-query";
import { getRouteApi, Link, useNavigate } from "@tanstack/react-router";
import { formatChf } from "../accounting/amount";
import { formatDe } from "../persons/format";
import { incomeStatementQueryOptions } from "./api";
import type { IncomeStatementView } from "./view";

const route = getRouteApi("/income-statement");

/**
 * The Income Statement (Erfolgsrechnung) page: the backend computes the whole tree,
 * the frontend only renders it. Title with the period subtitle; a toggle between
 * the simplified view (default) and the all view, stored in the URL `?view=` param.
 */
export function IncomeStatementPage() {
	const { view } = route.useSearch();
	const navigate = useNavigate();
	const { data: report, isPending } = useQuery(
		incomeStatementQueryOptions(view),
	);

	if (isPending || !report) {
		return <p className="p-8">Laden…</p>;
	}

	const period = `${formatDe(report.period.from)} – ${formatDe(report.period.to)}`;

	return (
		<div className="mx-auto mt-4 max-w-3xl px-4 pb-8">
			<h2 className="text-xl font-bold text-sky-900">Erfolgsrechnung</h2>
			<p className="text-sm text-sky-700">{period}</p>
			<ViewToggle
				view={view}
				onChange={(next) =>
					navigate({
						to: "/income-statement",
						search: { view: next },
					})
				}
			/>
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
							<div className="mt-2 flex justify-between border-t border-sky-200 pt-1 print:border-sky-900 text-sm font-semibold text-sky-900">
								<span>Gesamt {section.label}</span>
								<span className="tabular-nums">{formatChf(section.total)}</span>
							</div>
						</div>
					</section>
				))}
			</dl>
			<div className="mt-6 border-t-2 border-sky-300 pt-2 text-sm font-bold text-sky-900 print:mt-3">
				<div className="flex justify-between print:border-sky-900">
					<span>Gesamt Ertrag</span>
					<span className="tabular-nums">{formatChf(report.totalRevenue)}</span>
				</div>
				<div className="flex justify-between print:border-sky-900">
					<span>Gesamt Aufwand</span>
					<span className="tabular-nums">{formatChf(report.totalExpense)}</span>
				</div>
				<div className="mt-1 flex justify-between border-t border-sky-200 pt-1 text-base print:border-sky-900">
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

/** The view toggle: the choice lives in the URL `?view=` param, so links stay shareable. */
function ViewToggle({
	view,
	onChange,
}: {
	view: IncomeStatementView;
	onChange: (view: IncomeStatementView) => void;
}) {
	const options: [IncomeStatementView, string][] = [
		["simplified", "Vereinfacht"],
		["all", "Alle"],
	];
	return (
		<fieldset className="mt-4 flex gap-1 print:hidden" aria-label="Ansicht">
			{options.map(([value, label]) => (
				<button
					key={value}
					type="button"
					className={`rounded-lg px-3 py-1 text-sm ${
						value === view
							? "bg-sky-600 text-white"
							: "bg-sky-100 text-sky-800 hover:bg-sky-200"
					}`}
					aria-pressed={value === view}
					onClick={() => onChange(value)}
				>
					{label}
				</button>
			))}
		</fieldset>
	);
}

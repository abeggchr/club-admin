import { useSuspenseQuery } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";
import { settingsQueryOptions } from "../accounting/api";
import { textTemplatesQueryOptions } from "../settings/textTemplateApi";
import { InvoicesView } from "./InvoicesView";

export function InvoicesPage() {
	const { data: settings } = useSuspenseQuery(settingsQueryOptions);
	const { data: templates } = useSuspenseQuery(textTemplatesQueryOptions);
	const setupMissing =
		templates.length === 0 ||
		!settings ||
		[
			settings.iban,
			settings.name,
			settings.address,
			settings.postalCode,
			settings.city,
		].some((part) => !part.trim());

	return (
		<div className="px-8">
			<h2 className="mt-4 text-xl font-semibold text-sky-900">Rechnungen</h2>
			{setupMissing ? (
				<p className="mt-4">
					<Link
						to="/settings"
						className="text-sky-700 underline hover:text-sky-900"
					>
						Klubadresse und Textvorlagen in Einstellungen erfassen
					</Link>
				</p>
			) : (
				<InvoicesView />
			)}
		</div>
	);
}

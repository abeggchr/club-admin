import { useQueryClient, useSuspenseQuery } from "@tanstack/react-query";
import { type FormEvent, type ReactNode, useState } from "react";
import { formatChf, parseAmount } from "../accounting/amount";
import {
	invalidateJournalQueries,
	saveSettings,
	settingsQueryOptions,
} from "../accounting/api";
import { TextTemplatesSection } from "./TextTemplatesSection";

type FormState = {
	name: string;
	address: string;
	postalCode: string;
	city: string;
	iban: string;
	amount: string;
	startDate: string;
};

function Field({ label, children }: { label: string; children: ReactNode }) {
	return (
		<label className="flex flex-col gap-1 text-sm">
			{label}
			{children}
		</label>
	);
}

function FormSection({
	title,
	children,
}: {
	title: string;
	children: ReactNode;
}) {
	return (
		<fieldset className="flex flex-col gap-3">
			<legend className="mb-3 font-medium text-sky-900">{title}</legend>
			{children}
		</fieldset>
	);
}

const inputClass = "rounded border border-slate-300 p-2";

export function SettingsPage() {
	const queryClient = useQueryClient();
	const { data: settings } = useSuspenseQuery(settingsQueryOptions);
	const [form, setForm] = useState<FormState>({
		name: settings?.name ?? "",
		address: settings?.address ?? "",
		postalCode: settings?.postalCode ?? "",
		city: settings?.city ?? "",
		iban: settings?.iban ?? "",
		amount: settings ? formatChf(settings.openingBalance) : "",
		startDate: settings?.startDate ?? "",
	});
	const [error, setError] = useState<string | null>(null);
	const [saved, setSaved] = useState(false);

	const bind = (key: keyof FormState) => ({
		value: form[key],
		onChange: (e: { target: { value: string } }) =>
			setForm((prev) => ({ ...prev, [key]: e.target.value })),
		className: inputClass,
	});

	async function onSubmit(e: FormEvent) {
		e.preventDefault();
		setError(null);
		setSaved(false);
		const parsed = parseAmount(form.amount, { allowZero: true });
		if (!parsed.ok) {
			setError(parsed.error);
			return;
		}
		try {
			const result = await saveSettings({
				openingBalance: parsed.value,
				startDate: form.startDate,
				iban: form.iban.trim(),
				name: form.name.trim(),
				address: form.address.trim(),
				postalCode: form.postalCode.trim(),
				city: form.city.trim(),
			});
			queryClient.setQueryData(settingsQueryOptions.queryKey, result);
			await invalidateJournalQueries(queryClient);
			setForm((prev) => ({
				...prev,
				name: result.name,
				address: result.address,
				postalCode: result.postalCode,
				city: result.city,
				iban: result.iban,
				amount: formatChf(result.openingBalance),
			}));
			setSaved(true);
		} catch (err) {
			setError(
				err instanceof Error ? err.message : "Speichern fehlgeschlagen.",
			);
		}
	}

	return (
		<div className="px-8 pb-8">
			<h2 className="mt-4 text-xl font-semibold text-sky-900">Einstellungen</h2>
			<form
				onSubmit={onSubmit}
				noValidate
				className="mt-6 flex max-w-sm flex-col gap-6"
			>
				<FormSection title="Klub">
					<Field label="Name">
						<input required maxLength={70} {...bind("name")} />
					</Field>
					<Field label="Adresse">
						<input required maxLength={70} {...bind("address")} />
					</Field>
					<div className="grid grid-cols-[1fr_2fr] gap-3">
						<Field label="PLZ">
							<input required maxLength={16} {...bind("postalCode")} />
						</Field>
						<Field label="Ort">
							<input required maxLength={35} {...bind("city")} />
						</Field>
					</div>
				</FormSection>

				<FormSection title="Bankverbindung">
					<Field label="IBAN">
						<input required {...bind("iban")} />
					</Field>
				</FormSection>

				<FormSection title="Anfangssaldo">
					<Field label="Betrag">
						<input inputMode="decimal" {...bind("amount")} />
					</Field>
					<Field label="Startdatum">
						<input type="date" required {...bind("startDate")} />
					</Field>
				</FormSection>

				<div className="flex flex-col gap-3">
					<button
						type="submit"
						className="self-start rounded bg-sky-600 px-4 py-2 text-white hover:bg-sky-700"
					>
						Speichern
					</button>
					{saved && <p className="text-green-700">Gespeichert.</p>}
					{error && <p className="text-red-600">{error}</p>}
				</div>
			</form>
			<TextTemplatesSection />
		</div>
	);
}

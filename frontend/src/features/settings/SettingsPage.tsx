import { useQueryClient, useSuspenseQuery } from "@tanstack/react-query";
import { type FormEvent, useState } from "react";
import { formatChf, parseAmount } from "../accounting/amount";
import {
	invalidateJournalQueries,
	saveSettings,
	settingsQueryOptions,
} from "../accounting/api";
import { TextTemplatesSection } from "./TextTemplatesSection";

export function SettingsPage() {
	const queryClient = useQueryClient();
	const { data: settings } = useSuspenseQuery(settingsQueryOptions);
	const [amount, setAmount] = useState(
		settings ? formatChf(settings.openingBalance) : "",
	);
	const [startDate, setStartDate] = useState(settings?.startDate ?? "");
	const [iban, setIban] = useState(settings?.iban ?? "");
	const [name, setName] = useState(settings?.name ?? "");
	const [address, setAddress] = useState(settings?.address ?? "");
	const [postalCode, setPostalCode] = useState(settings?.postalCode ?? "");
	const [city, setCity] = useState(settings?.city ?? "");
	const [error, setError] = useState<string | null>(null);
	const [saved, setSaved] = useState(false);

	async function onSubmit(e: FormEvent) {
		e.preventDefault();
		setError(null);
		setSaved(false);
		const parsed = parseAmount(amount, { allowZero: true });
		if (!parsed.ok) {
			setError(parsed.error);
			return;
		}
		try {
			const result = await saveSettings({
				openingBalance: parsed.value,
				startDate,
				iban: iban.trim(),
				name: name.trim(),
				address: address.trim(),
				postalCode: postalCode.trim(),
				city: city.trim(),
			});
			queryClient.setQueryData(settingsQueryOptions.queryKey, result);
			await invalidateJournalQueries(queryClient);
			setAmount(formatChf(result.openingBalance));
			setIban(result.iban);
			setName(result.name);
			setAddress(result.address);
			setPostalCode(result.postalCode);
			setCity(result.city);
			setSaved(true);
		} catch (err) {
			setError(
				err instanceof Error ? err.message : "Speichern fehlgeschlagen.",
			);
		}
	}

	return (
		<div className="px-8">
			<h2 className="mt-4 text-xl font-semibold text-sky-900">Einstellungen</h2>
			<form
				onSubmit={onSubmit}
				noValidate
				className="mt-6 flex max-w-sm flex-col gap-3"
			>
				<label className="flex flex-col text-sm">
					Name
					<input
						required
						maxLength={70}
						value={name}
						onChange={(e) => setName(e.target.value)}
						className="rounded border border-slate-300 p-2"
					/>
				</label>
				<h3 className="mt-2 font-medium">Anfangssaldo</h3>
				<label className="flex flex-col text-sm">
					Betrag
					<input
						inputMode="decimal"
						value={amount}
						onChange={(e) => setAmount(e.target.value)}
						className="rounded border border-slate-300 p-2"
					/>
				</label>
				<label className="flex flex-col text-sm">
					Startdatum
					<input
						type="date"
						required
						value={startDate}
						onChange={(e) => setStartDate(e.target.value)}
						className="rounded border border-slate-300 p-2"
					/>
				</label>
				<label className="flex flex-col text-sm">
					IBAN
					<input
						required
						value={iban}
						onChange={(e) => setIban(e.target.value)}
						className="rounded border border-slate-300 p-2"
					/>
				</label>
				<h3 className="mt-2 font-medium">Klubadresse</h3>
				<label className="flex flex-col text-sm">
					Adresse
					<input
						required
						maxLength={70}
						value={address}
						onChange={(e) => setAddress(e.target.value)}
						className="rounded border border-slate-300 p-2"
					/>
				</label>
				<label className="flex flex-col text-sm">
					PLZ
					<input
						required
						maxLength={16}
						value={postalCode}
						onChange={(e) => setPostalCode(e.target.value)}
						className="rounded border border-slate-300 p-2"
					/>
				</label>
				<label className="flex flex-col text-sm">
					Ort
					<input
						required
						maxLength={35}
						value={city}
						onChange={(e) => setCity(e.target.value)}
						className="rounded border border-slate-300 p-2"
					/>
				</label>
				<button
					type="submit"
					className="self-start rounded bg-sky-600 px-4 py-2 text-white hover:bg-sky-700"
				>
					Speichern
				</button>
				{saved && <p className="text-green-700">Gespeichert.</p>}
				{error && <p className="text-red-600">{error}</p>}
			</form>
			<TextTemplatesSection />
		</div>
	);
}

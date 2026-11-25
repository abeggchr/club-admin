import { useQueryClient, useSuspenseQuery } from "@tanstack/react-query";
import { type FormEvent, type ReactNode, useEffect, useState } from "react";
import { formatChf, parseAmount } from "../accounting/amount";
import {
	invalidateJournalQueries,
	saveSettings,
	sendTestEmail,
	settingsQueryOptions,
} from "../accounting/api";
import {
	emptySmtpDraft,
	type SmtpDraft,
	type SmtpSecurity,
	smtpDraftFrom,
	smtpPayload,
} from "./smtpDraft";
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
		// biome-ignore lint/a11y/noLabelWithoutControl: the input is passed in as children
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
		<fieldset className="flex flex-col gap-3 rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
			<legend className="px-2 font-medium text-sky-900">{title}</legend>
			{children}
		</fieldset>
	);
}

const securityOptions: [SmtpSecurity, string][] = [
	["StartTls", "StartTLS"],
	["Ssl", "SSL"],
	["None", "Keine"],
];

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
	const [smtp, setSmtp] = useState<SmtpDraft>(
		settings ? smtpDraftFrom(settings.smtp) : emptySmtpDraft,
	);
	const [ePostApiKey, setEPostApiKey] = useState("");
	const [receiver, setReceiver] = useState("");
	const [testResult, setTestResult] = useState<{
		ok: boolean;
		message: string;
	} | null>(null);
	const [error, setError] = useState<string | null>(null);
	const [saved, setSaved] = useState(false);

	useEffect(() => {
		if (!testResult?.ok) return;
		const timer = setTimeout(() => setTestResult(null), 5_000);
		return () => clearTimeout(timer);
	}, [testResult]);

	const bind = (key: keyof FormState) => ({
		value: form[key],
		onChange: (e: { target: { value: string } }) =>
			setForm((prev) => ({ ...prev, [key]: e.target.value })),
		className: inputClass,
	});

	const bindSmtp = (key: keyof SmtpDraft) => ({
		value: smtp[key],
		onChange: (e: { target: { value: string } }) =>
			setSmtp((prev) => ({ ...prev, [key]: e.target.value })),
		className: inputClass,
	});

	// The receiver is pre-filled with the from address until the user types one.
	const testReceiver = receiver || smtp.fromAddress;

	async function onSendTest() {
		setTestResult(null);
		const payload = smtpPayload(smtp);
		if (!payload.ok) {
			setTestResult({ ok: false, message: payload.error });
			return;
		}
		try {
			await sendTestEmail(testReceiver.trim(), payload.value);
			setTestResult({
				ok: true,
				message: `Test-E-Mail gesendet an ${testReceiver.trim()}.`,
			});
		} catch (err) {
			setTestResult({
				ok: false,
				message: err instanceof Error ? err.message : "Senden fehlgeschlagen.",
			});
		}
	}

	async function onSubmit(e: FormEvent) {
		e.preventDefault();
		setError(null);
		setSaved(false);
		const parsed = parseAmount(form.amount, { allowZero: true });
		if (!parsed.ok) {
			setError(parsed.error);
			return;
		}
		const smtpParsed = smtpPayload(smtp);
		if (!smtpParsed.ok) {
			setError(smtpParsed.error);
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
				smtp: smtpParsed.value,
				ePostApiKey: ePostApiKey.trim(),
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
			setSmtp(smtpDraftFrom(result.smtp));
			setEPostApiKey("");
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
				className="mt-6 grid grid-cols-1 items-start gap-6 md:grid-cols-2 xl:grid-cols-3"
			>
				<div className="flex flex-col gap-6">
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
				</div>

				<FormSection title="E-Mail (SMTP)">
					<p className="text-sm text-slate-600">
						Optional: entweder alle Pflichtfelder ausfüllen oder alle leer
						lassen.
					</p>
					<Field label="Server">
						<input {...bindSmtp("host")} />
					</Field>
					<div className="grid grid-cols-[1fr_2fr] gap-3">
						<Field label="Port">
							<input inputMode="numeric" {...bindSmtp("port")} />
						</Field>
						<Field label="Verschlüsselung">
							<select {...bindSmtp("security")}>
								<option value="" />
								{securityOptions.map(([value, label]) => (
									<option key={value} value={value}>
										{label}
									</option>
								))}
							</select>
						</Field>
					</div>
					<Field label="Benutzername">
						<input autoComplete="off" {...bindSmtp("username")} />
					</Field>
					<Field
						label={`Passwort (${settings?.smtp.passwordSet ? "gesetzt" : "nicht gesetzt"})`}
					>
						<input
							type="password"
							autoComplete="new-password"
							placeholder={
								settings?.smtp.passwordSet ? "leer lassen = unverändert" : ""
							}
							{...bindSmtp("password")}
						/>
					</Field>
					<Field label="Absenderadresse">
						<input type="email" {...bindSmtp("fromAddress")} />
					</Field>
					<Field label="Absendername">
						<input {...bindSmtp("fromName")} />
					</Field>
					<Field label="Empfänger der Test-E-Mail">
						<input
							type="email"
							className={inputClass}
							value={testReceiver}
							onChange={(e) => setReceiver(e.target.value)}
						/>
					</Field>
					<button
						type="button"
						onClick={onSendTest}
						className="self-start rounded border border-sky-600 px-4 py-2 text-sky-700 hover:bg-sky-50"
					>
						Test-E-Mail senden
					</button>
					{testResult && (
						<p className={testResult.ok ? "text-green-700" : "text-red-600"}>
							{testResult.message}
						</p>
					)}
				</FormSection>

				<div className="flex flex-col gap-6">
					<FormSection title="Post (ePost)">
						<p className="text-sm text-slate-600">
							Optional: erlaubt den Versand von Rechnungen als Brief. Jeder
							Versand wird verrechnet, einen Test gibt es nicht.
						</p>
						<Field
							label={`API-Key (${settings?.ePostConfigured ? "gesetzt" : "nicht gesetzt"})`}
						>
							<input
								type="password"
								autoComplete="new-password"
								placeholder={
									settings?.ePostConfigured ? "leer lassen = unverändert" : ""
								}
								className={inputClass}
								value={ePostApiKey}
								onChange={(e) => setEPostApiKey(e.target.value)}
							/>
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
				</div>
			</form>
			<TextTemplatesSection />
		</div>
	);
}

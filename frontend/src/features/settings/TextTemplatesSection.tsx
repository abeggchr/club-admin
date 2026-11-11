import { useQueryClient, useSuspenseQuery } from "@tanstack/react-query";
import { type FormEvent, useState } from "react";
import {
	deleteTextTemplate,
	saveTextTemplate,
	type TextTemplate,
	textTemplatesQueryOptions,
} from "./textTemplateApi";
import {
	emptyTextTemplateDraft,
	type TextTemplateDraft,
	textTemplatePlaceholders,
	validateTextTemplate,
} from "./textTemplateDraft";

type Editing = { id: number | null; draft: TextTemplateDraft };

const inputClass = "rounded border border-slate-300 p-2";

export function TextTemplatesSection() {
	const queryClient = useQueryClient();
	const { data: templates } = useSuspenseQuery(textTemplatesQueryOptions);
	const [editing, setEditing] = useState<Editing | null>(null);
	const [error, setError] = useState<string | null>(null);

	const refresh = () =>
		queryClient.invalidateQueries({
			queryKey: textTemplatesQueryOptions.queryKey,
		});

	function open(template: TextTemplate | null) {
		setError(null);
		setEditing(
			template
				? {
						id: template.id,
						draft: {
							subject: template.subject,
							text: template.text,
							greetings: template.greetings,
						},
					}
				: { id: null, draft: emptyTextTemplateDraft },
		);
	}

	function change(field: keyof TextTemplateDraft, value: string) {
		setEditing((e) =>
			e ? { ...e, draft: { ...e.draft, [field]: value } } : e,
		);
	}

	async function onSubmit(e: FormEvent) {
		e.preventDefault();
		if (!editing) return;
		const invalid = validateTextTemplate(editing.draft);
		if (invalid) {
			setError(invalid);
			return;
		}
		try {
			await saveTextTemplate(editing.id, editing.draft);
			await refresh();
			setEditing(null);
			setError(null);
		} catch (err) {
			setError(
				err instanceof Error ? err.message : "Speichern fehlgeschlagen.",
			);
		}
	}

	async function onDelete() {
		if (!editing || editing.id === null) return;
		if (!window.confirm("Textvorlage wirklich löschen?")) return;
		try {
			await deleteTextTemplate(editing.id);
			await refresh();
			setEditing(null);
			setError(null);
		} catch (err) {
			setError(err instanceof Error ? err.message : "Löschen fehlgeschlagen.");
		}
	}

	return (
		<section className="mt-10 flex max-w-xl flex-col gap-3">
			<div className="flex items-center gap-3">
				<h3 className="font-medium">Textvorlagen</h3>
				<button
					type="button"
					aria-label="Textvorlage hinzufügen"
					onClick={() => open(null)}
					className="rounded bg-sky-600 px-3 py-1 text-white hover:bg-sky-700"
				>
					+
				</button>
			</div>
			<details className="text-sm text-slate-600">
				<summary className="cursor-pointer">Verfügbare Platzhalter</summary>
				<ul className="mt-1 flex flex-col gap-0.5">
					{textTemplatePlaceholders.map((p) => (
						<li key={p.key}>
							<code>{`{{${p.key}}}`}</code> – {p.description}
						</li>
					))}
				</ul>
			</details>
			{templates.length === 0 && (
				<p className="text-sm text-slate-500">Keine Textvorlagen vorhanden.</p>
			)}
			<ul className="flex flex-col gap-1">
				{templates.map((t) => (
					<li key={t.id}>
						<button
							type="button"
							onClick={() => open(t)}
							className={`w-full rounded px-3 py-2 text-left hover:bg-sky-50 ${editing?.id === t.id ? "bg-sky-100" : ""}`}
						>
							{t.subject}
						</button>
					</li>
				))}
			</ul>
			{editing && (
				<form
					onSubmit={onSubmit}
					noValidate
					className="flex flex-col gap-3 rounded border border-slate-200 p-4"
				>
					<label className="flex flex-col text-sm">
						Betreff
						<input
							value={editing.draft.subject}
							onChange={(e) => change("subject", e.target.value)}
							className={inputClass}
						/>
					</label>
					<label className="flex flex-col text-sm">
						Text
						<textarea
							rows={5}
							value={editing.draft.text}
							onChange={(e) => change("text", e.target.value)}
							className={inputClass}
						/>
					</label>
					<label className="flex flex-col text-sm">
						Grüsse
						<textarea
							rows={3}
							value={editing.draft.greetings}
							onChange={(e) => change("greetings", e.target.value)}
							className={inputClass}
						/>
					</label>
					<div className="flex gap-2">
						<button
							type="submit"
							className="rounded bg-sky-600 px-4 py-2 text-white hover:bg-sky-700"
						>
							Speichern
						</button>
						<button
							type="button"
							onClick={() => setEditing(null)}
							className="rounded border border-slate-300 px-4 py-2"
						>
							Abbrechen
						</button>
						{editing.id !== null && (
							<button
								type="button"
								onClick={onDelete}
								className="ml-auto rounded border border-red-300 px-4 py-2 text-red-700"
							>
								Löschen
							</button>
						)}
					</div>
					{error && <p className="text-red-600">{error}</p>}
				</form>
			)}
		</section>
	);
}

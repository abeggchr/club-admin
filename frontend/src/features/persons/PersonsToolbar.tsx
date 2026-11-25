import { FiDownload, FiUpload } from "react-icons/fi";
import { type ViewKey, views } from "./views";

type Props = {
	filter: string;
	onFilterChange: (value: string) => void;
	viewKey: ViewKey;
	onViewChange: (key: ViewKey) => void;
	onExport: () => void;
	onMailingList: () => void;
	onImport: () => void;
};

export function PersonsToolbar({
	filter,
	onFilterChange,
	viewKey,
	onViewChange,
	onExport,
	onMailingList,
	onImport,
}: Props) {
	return (
		<div className="mt-4 flex items-center gap-4">
			<input
				type="search"
				placeholder="Suchen (Name, E-Mail, Ort, Land, Notizen)…"
				value={filter}
				onChange={(e) => onFilterChange(e.target.value)}
				className="w-96 rounded border border-neutral-300 px-3 py-2 text-sm"
			/>
			<fieldset className="inline-flex">
				<legend className="sr-only">Ansicht</legend>
				{Object.entries(views).map(([key, v]) => (
					<button
						key={key}
						type="button"
						aria-pressed={key === viewKey}
						onClick={() => onViewChange(key as ViewKey)}
						className={`-ml-px border border-neutral-300 px-3 py-2 text-sm first:ml-0 first:rounded-l last:rounded-r ${
							key === viewKey
								? "bg-neutral-800 text-white"
								: "hover:bg-neutral-50"
						}`}
					>
						{v.label}
					</button>
				))}
			</fieldset>
			<div className="ml-auto flex gap-2">
				<button
					type="button"
					onClick={onExport}
					className="flex items-center gap-2 rounded border border-sky-600 px-3 py-2 text-sm text-sky-700 hover:bg-sky-50"
				>
					<FiDownload />
					Export
				</button>
				<button
					type="button"
					onClick={onMailingList}
					className="flex items-center gap-2 rounded border border-sky-600 px-3 py-2 text-sm text-sky-700 hover:bg-sky-50"
				>
					<FiDownload />
					Versandliste
				</button>
				<button
					type="button"
					onClick={onImport}
					className="flex items-center gap-2 rounded border border-sky-600 px-3 py-2 text-sm text-sky-700 hover:bg-sky-50"
				>
					<FiUpload />
					Import
				</button>
			</div>
		</div>
	);
}

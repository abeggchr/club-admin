import { useSuspenseQuery } from "@tanstack/react-query";
import { getRouteApi } from "@tanstack/react-router";
import type { SortingState } from "@tanstack/react-table";
import { useTable } from "@tanstack/react-table";
import { useEffect, useMemo, useRef, useState } from "react";
import { personsQueryOptions } from "./api";
import { features } from "./columns";
import { downloadPersonCsv } from "./csv";
import { EditPanel } from "./EditPanel";
import { ImportDialog } from "./ImportDialog";
import { PersonsTable } from "./PersonsTable";
import { PersonsToolbar } from "./PersonsToolbar";
import { globalSearchFilter } from "./search";
import { type ViewKey, views } from "./views";

const route = getRouteApi("/persons");

export function PersonsPage() {
	const { person: linkedPersonId } = route.useSearch();
	const { data: persons } = useSuspenseQuery(personsQueryOptions);
	const [sorting, setSorting] = useState<SortingState>([
		{ id: "lastName", desc: false },
		{ id: "firstName", desc: false },
	]);
	const [filter, setFilter] = useState("");
	const [debouncedFilter, setDebouncedFilter] = useState("");
	const [viewKey, setViewKey] = useState<ViewKey>("active");
	const [selectedId, setSelectedId] = useState<number | null>(
		linkedPersonId ?? null,
	);
	const selected = persons.find((p) => p.id === selectedId) ?? null;
	const importDialog = useRef<HTMLDialogElement>(null);
	const view = views[viewKey];
	const viewPersons = useMemo(
		() => ("filter" in view ? persons.filter(view.filter) : persons),
		[persons, view],
	);

	useEffect(() => {
		const timer = setTimeout(() => setDebouncedFilter(filter), 300);
		return () => clearTimeout(timer);
	}, [filter]);

	const table = useTable({
		features,
		columns: view.columns,
		data: viewPersons,
		state: { sorting, globalFilter: debouncedFilter },
		onSortingChange: setSorting,
		onGlobalFilterChange: setDebouncedFilter,
		globalFilterFn: globalSearchFilter,
		defaultColumn: { sortFn: "alphanumeric" },
		initialState: { pagination: { pageIndex: 0, pageSize: 25 } },
	});

	return (
		<div className="px-8">
			<PersonsToolbar
				filter={filter}
				onFilterChange={setFilter}
				viewKey={viewKey}
				onViewChange={setViewKey}
				onExport={() =>
					downloadPersonCsv(
						table.getFilteredRowModel().rows.map((r) => r.original),
					)
				}
				onImport={() => importDialog.current?.showModal()}
			/>
			<ImportDialog dialogRef={importDialog} />

			<div className="mt-4 flex items-start gap-4">
				<PersonsTable
					table={table}
					emptyMessage={
						persons.length === 0
							? "Keine Personen vorhanden."
							: "Keine Personen gefunden."
					}
					selectedId={selectedId}
					onSelect={setSelectedId}
				/>
				{selected && (
					<EditPanel
						key={selected.id}
						person={selected}
						onClose={() => setSelectedId(null)}
					/>
				)}
			</div>
		</div>
	);
}

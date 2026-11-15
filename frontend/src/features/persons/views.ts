import { columns } from "./columns";
import type { Person } from "./types";

function hideColumns(...keys: string[]) {
	return columns.filter(
		(c) => !("accessorKey" in c && keys.includes(c.accessorKey)),
	);
}

export const views = {
	active: {
		label: "Aktive",
		filter: (p: Person) => !!p.joinedOn && !p.leftOn,
		columns: hideColumns(
			"leftOn",
			"dateOfBirth",
			"joinedOn",
			"landlinePhone",
			"country",
			"notes",
			"receivesClubNews"
		),
	},
	contacts: {
		label: "Kontakte",
		filter: (p: Person) => !p.joinedOn,
		columns: hideColumns(
			"dateOfBirth",
			"membership",
			"joinedOn",
			"leftOn",
			"country",
			"notes",
		),
	},
	former: {
		label: "Ehemalige",
		filter: (p: Person) => !!p.leftOn,
		columns: hideColumns("membership", "dateOfBirth", "country", "notes"),
	},
	all: {
		label: "Alle",
		columns,
	},
};

export type ViewKey = keyof typeof views;

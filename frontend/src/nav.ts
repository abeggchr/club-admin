import {
	MdAccountBalance,
	MdAssessment,
	MdPeople,
	MdReceipt,
	MdSettings,
} from "react-icons/md";

export const navItems = [
	{ to: "/persons", label: "Persons", Icon: MdPeople, align: "start" },
	{ to: "/invoices", label: "Rechnungen", Icon: MdReceipt, align: "start" },
	{
		to: "/accounting",
		label: "Buchhaltung",
		Icon: MdAccountBalance,
		align: "start",
	},
	{
		to: "/income-statement",
		label: "Erfolgsrechnung",
		Icon: MdAssessment,
		align: "start",
	},
	{ to: "/settings", label: "Einstellungen", Icon: MdSettings, align: "end" },
] as const;

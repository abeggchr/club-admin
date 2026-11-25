/** The Income Statement (Erfolgsrechnung) tree as computed by the backend; the frontend only renders it. */

export type IncomeStatementLine = {
	label: string;
	amount: number;
};

export type IncomeStatementGroup = {
	label: string;
	total: number;
	lines: IncomeStatementLine[];
};

export type IncomeStatementSection = {
	label: string;
	total: number;
	groups: IncomeStatementGroup[];
};

export type IncomeStatement = {
	period: {
		from: string;
		to: string;
	};
	sections: IncomeStatementSection[];
	totalRevenue: number;
	totalExpense: number;
	overallResult: number;
};

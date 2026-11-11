import { describe, expect, it } from "vitest";
import { highlightOf, toggleSelection, type Selection } from "./selection";

const entries = [
	{ id: 1, transactionId: 10 },
	{ id: 2, transactionId: 10 },
	{ id: 3, transactionId: 11 },
];

describe("highlightOf", () => {
	it("highlights nothing without a selection", () => {
		expect(highlightOf(null, entries)).toEqual({
			transactionIds: [],
			entryIds: [],
		});
	});

	it("selecting a Bewegung highlights all its Buchungen", () => {
		expect(highlightOf({ kind: "transaction", id: 10 }, entries)).toEqual({
			transactionIds: [],
			entryIds: [1, 2],
		});
	});

	it("selecting a Buchung highlights its siblings and its Bewegung, not itself", () => {
		expect(
			highlightOf({ kind: "entry", id: 1, transactionId: 10 }, entries),
		).toEqual({
			transactionIds: [10],
			entryIds: [2],
		});
	});
});

describe("toggleSelection", () => {
	const t: Selection = { kind: "transaction", id: 10 };
	const e: Selection = { kind: "entry", id: 1, transactionId: 10 };

	it("selects the clicked row", () => {
		expect(toggleSelection(null, t)).toEqual(t);
	});

	it("moves the selection to another row, also across kinds", () => {
		expect(toggleSelection(t, e)).toEqual(e);
		expect(toggleSelection(e, { kind: "transaction", id: 10 })).toEqual({
			kind: "transaction",
			id: 10,
		});
	});

	it("clears when the selected row is clicked again", () => {
		expect(toggleSelection(t, { ...t })).toBeNull();
		expect(toggleSelection(e, { ...e })).toBeNull();
	});
});

import { describe, expect, it } from "vitest";
import { pieSlices } from "./pie";

describe("pieSlices", () => {
	it("returns no slices for no values and for all-zero values", () => {
		expect(pieSlices([])).toEqual([]);
		expect(pieSlices([0, 0])).toEqual([]);
	});

	it("gives a single value the full circle", () => {
		const [slice] = pieSlices([5]);
		expect(slice.size).toBe(100);
		expect(slice.points.split(" ")).toHaveLength(3);
	});

	it("splits equal values into equal arcs", () => {
		const [a, b] = pieSlices([1, 1]);
		expect(a.size).toBe(50);
		expect(b.size).toBe(50);
	});

	it("keeps the arc proportional to the value", () => {
		const [big, small] = pieSlices([3, 1]);
		expect(big.size).toBe(75);
		expect(small.size).toBe(25);
	});

	it("starts at 12 o'clock and goes clockwise: the first slice's edge is 50,2", () => {
		const [a] = pieSlices([1, 1]);
		const [, start, end] = a.points.split(" ");
		expect(start).toBe("50,2");
		expect(end).toBe("50,98");
	});

	it("is seamless: the next slice starts where the previous one ends", () => {
		const [a, b] = pieSlices([1, 1]);
		const [, , endA] = a.points.split(" ");
		const [, startB] = b.points.split(" ");
		expect(startB).toBe(endA);
	});

	it("returns one slice per value in the given order", () => {
		const slices = pieSlices([1, 2, 3]);
		expect(slices.map((s) => s.index)).toEqual([0, 1, 2]);
		expect(slices.map((s) => s.size)).toEqual([16.67, 33.33, 50]);
	});
});

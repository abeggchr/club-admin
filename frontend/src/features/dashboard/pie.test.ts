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
		expect(slice.path).toBe(
			"M50,50 L50,2 A48,48 0 0 1 50,98 A48,48 0 0 1 50,2 Z",
		);
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

	it("starts at 12 o'clock and goes clockwise, closing with a circular arc", () => {
		const [a] = pieSlices([1, 1]);
		expect(a.path).toBe("M50,50 L50,2 A48,48 0 0 1 50,98 Z");
	});

	it("is seamless: the next slice starts where the previous one ends", () => {
		const [a, b] = pieSlices([1, 1]);
		expect(a.path).toContain("50,98 Z");
		expect(b.path).toContain("L50,98 ");
	});

	it("flags arcs wider than 180° as large", () => {
		const [big] = pieSlices([3, 1]);
		expect(big.path).toContain("A48,48 0 1 1");
	});

	it("returns one slice per value in the given order", () => {
		const slices = pieSlices([1, 2, 3]);
		expect(slices.map((s) => s.index)).toEqual([0, 1, 2]);
		expect(slices.map((s) => s.size)).toEqual([16.67, 33.33, 50]);
	});
});

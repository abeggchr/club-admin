// The hand-written SVG pie of the dashboard: the slice geometry is computed here (and tested),
// the component only draws it.

export type PieSlice = {
	index: number;
	/** The slice's span in degrees, always > 0. */
	size: number;
	/** "x1,y1 x2,y2 x3,y3 …" points for an SVG <polygon>. */
	points: string;
};

const CENTER = 50;
const RADIUS = 48;
const SIZE = 100;

const round = (n: number) => Math.round(n * 100) / 100;

/**
 * One slice per value, in the given order, starting at 12 o'clock and going clockwise.
 * The angles are derived from the values directly: equal values always give equal arcs,
 * and the last point of a slice is the first point of the next one (no hairline gaps).
 */
export function pieSlices(values: number[]): PieSlice[] {
	const total = values.reduce((sum, v) => sum + v, 0);
	if (total <= 0) return [];
	const pointsPerSlice = values.map((v) => (v / total) * 360);
	return values.map((_, i) => {
		const start = pointsPerSlice.slice(0, i).reduce((sum, v) => sum + v, 0);
		const end = start + pointsPerSlice[i];
		const toXY = (deg: number) => {
			// 0° is 12 o'clock, increasing clockwise.
			const rad = ((deg - 90) * Math.PI) / 180;
			return `${round(CENTER + RADIUS * Math.cos(rad))},${round(CENTER + RADIUS * Math.sin(rad))}`;
		};
		const points = [`${CENTER},${CENTER}`, toXY(start), toXY(end)].join(" ");
		return { index: i, size: round((pointsPerSlice[i] / 360) * 100), points };
	});
}

export const PIE_SIZE = SIZE;

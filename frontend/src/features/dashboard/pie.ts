// The hand-written SVG pie of the dashboard: the slice geometry is computed here (and tested),
// the component only draws it.

export type PieSlice = {
	index: number;
	/** The slice's span in degrees, always > 0. */
	size: number;
	/** The "d" attribute of an SVG <path>: the center, a straight edge and a circular arc. */
	path: string;
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
		const arc = (to: string, large: number) =>
			`A${RADIUS},${RADIUS} 0 ${large} 1 ${to}`;
		const span = pointsPerSlice[i];
		// An arc can't span 360° (start and end coincide), so a full circle is drawn as two halves.
		const arcs =
			span >= 360
				? `${arc(toXY(start + 180), 0)} ${arc(toXY(end), 0)}`
				: arc(toXY(end), span > 180 ? 1 : 0);
		const path = `M${CENTER},${CENTER} L${toXY(start)} ${arcs} Z`;
		return { index: i, size: round((span / 360) * 100), path };
	});
}

export const PIE_SIZE = SIZE;

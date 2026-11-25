import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";

// The print layout is a small @media print block in styles.css plus print: variants on the
// page components; these tests pin the rules that make printing clean.
const testDir = dirname(fileURLToPath(import.meta.url));
const styles = readFileSync(join(testDir, "styles.css"), "utf8");

describe("Print layout", () => {
	it("Hides the TanStack devtools in print", () => {
		const printBlock = styles.slice(styles.indexOf("@media print"));
		expect(printBlock).toContain("#tanstack_devtools");
		expect(printBlock).toContain("display: none");
	});

	it("Does not hide the report body in print", () => {
		const printBlock = styles.slice(styles.indexOf("@media print"));
		expect(printBlock).not.toContain("#app");
		expect(printBlock).not.toContain("body *");
	});
});

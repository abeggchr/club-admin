import { expect, test } from "vitest";
import { loginUrl } from "./auth";

test("login url carries the encoded page to return to", () => {
	expect(loginUrl("/persons?q=a b#top")).toBe(
		"/api/auth/login?returnUrl=%2Fpersons%3Fq%3Da%20b%23top",
	);
});

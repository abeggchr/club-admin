import { afterEach, describe, expect, it, vi } from "vitest";
import { apiFetch } from "./apiFetch";

describe("apiFetch", () => {
	afterEach(() => vi.unstubAllGlobals());

	it("forwards url and init to fetch and returns its response", async () => {
		const response = new Response("ok");
		const fetchMock = vi.fn().mockResolvedValue(response);
		vi.stubGlobal("fetch", fetchMock);

		const result = await apiFetch("/api/persons", { method: "DELETE" });

		expect(fetchMock).toHaveBeenCalledWith("/api/persons", {
			method: "DELETE",
		});
		expect(result).toBe(response);
	});
});

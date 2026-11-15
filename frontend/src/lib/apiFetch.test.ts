import { afterEach, describe, expect, it, vi } from "vitest";
import { apiFetch } from "./apiFetch";

describe("apiFetch", () => {
	afterEach(() => vi.unstubAllGlobals());

	it("forwards url and init to fetch and returns its response", async () => {
		const response = new Response("ok");
		const fetchMock = vi.fn().mockResolvedValue(response);
		vi.stubGlobal("fetch", fetchMock);

		const result = await apiFetch("/api/person", { method: "DELETE" });

		expect(fetchMock).toHaveBeenCalledWith("/api/person", {
			method: "DELETE",
		});
		expect(result).toBe(response);
	});
});

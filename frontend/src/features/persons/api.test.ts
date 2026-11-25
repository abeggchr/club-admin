import { afterEach, describe, expect, it, vi } from "vitest";
import { deletePerson } from "./api";

describe("deletePerson", () => {
	afterEach(() => vi.unstubAllGlobals());

	it("resolves without throwing when the backend answers 204", async () => {
		const fetchMock = vi
			.fn()
			.mockResolvedValue(new Response(null, { status: 204 }));
		vi.stubGlobal("fetch", fetchMock);

		await expect(deletePerson(1)).resolves.toBeUndefined();
		expect(fetchMock).toHaveBeenCalledWith("/api/person/1", {
			method: "DELETE",
		});
	});

	it("throws the backend's German error when the Person is someone's Zahler", async () => {
		const error =
			"Die Person ist Zahler für andere Personen und kann nicht gelöscht werden.";
		const fetchMock = vi.fn().mockResolvedValue(
			new Response(JSON.stringify({ error }), {
				status: 400,
			}),
		);
		vi.stubGlobal("fetch", fetchMock);

		await expect(deletePerson(1)).rejects.toThrow(error);
	});

	it("throws a generic message when the Person does not exist", async () => {
		const fetchMock = vi
			.fn()
			.mockResolvedValue(new Response(null, { status: 404 }));
		vi.stubGlobal("fetch", fetchMock);

		await expect(deletePerson(999)).rejects.toThrow("Backend returned 404");
	});
});

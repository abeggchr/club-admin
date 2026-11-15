/** The single entry point for every call to the backend's /api. */
export function apiFetch(input: string, init?: RequestInit): Promise<Response> {
	return fetch(input, init);
}

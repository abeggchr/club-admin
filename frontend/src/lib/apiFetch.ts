import { redirectToLogin } from "./auth";

/** The single entry point for every call to the backend's /api. A 401 means the login expired. */
export async function apiFetch(
	input: string,
	init?: RequestInit,
): Promise<Response> {
	const response = await fetch(input, init);
	if (response.status === 401) return redirectToLogin();
	return response;
}

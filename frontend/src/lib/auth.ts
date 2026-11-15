export type CurrentUser = { name: string | null; email: string | null };

export function loginUrl(returnUrl: string): string {
	return `/api/auth/login?returnUrl=${encodeURIComponent(returnUrl)}`;
}

/** Full-page navigation to the Microsoft login; the page never settles afterwards. */
export function redirectToLogin(): Promise<never> {
	const { pathname, search, hash } = window.location;
	window.location.assign(loginUrl(pathname + search + hash));
	return new Promise<never>(() => {});
}

/** The signed-in user, or a redirect to the login when there is none. */
export async function fetchCurrentUser(): Promise<CurrentUser> {
	const response = await fetch("/api/auth/me");
	if (response.status === 401) return redirectToLogin();
	if (!response.ok)
		throw new Error(`Anmeldung fehlgeschlagen (${response.status})`);
	return response.json();
}

export async function logout(): Promise<void> {
	await fetch("/api/auth/logout", { method: "POST" });
	window.location.assign("/");
}

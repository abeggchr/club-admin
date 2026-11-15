import tailwindcss from "@tailwindcss/vite";
import { devtools } from "@tanstack/devtools-vite";

import { tanstackRouter } from "@tanstack/router-plugin/vite";

import viteReact from "@vitejs/plugin-react";
import { defineConfig, type ProxyOptions } from "vite";

// Vite's proxy replaces the Host header with the target's, so the backend would build
// http://localhost:5210/signin-oidc as the redirect URI and the browser would land on the
// backend port after login. Forward the browser's original Host instead.
const backendProxy: ProxyOptions = {
	target: "http://localhost:5210",
	configure: (proxy) => {
		proxy.on("proxyReq", (proxyReq, req) => {
			if (req.headers.host) proxyReq.setHeader("host", req.headers.host);
		});
	},
};

const config = defineConfig({
	resolve: { tsconfigPaths: true },
	server: {
		proxy: {
			"/api": backendProxy,
			// Entra redirects back here.
			"/signin-oidc": backendProxy,
		},
	},
	plugins: [
		devtools(),
		tailwindcss(),
		tanstackRouter({ target: "react", autoCodeSplitting: true }),
		viteReact(),
	],
});

export default config;

import createClient from "openapi-fetch";
import type { paths } from "./schema";

/**
 * Typed API client generated from the backend's OpenAPI document.
 * Run `npm run gen:api` after changing the API. Never hand-write DTOs.
 *
 * Requests go to the same origin; Next.js proxies /api to the .NET API.
 * The X-Pantrio-Csrf header is required by the API on every unsafe method.
 */
export const api = createClient<paths>({
  baseUrl: typeof window === "undefined" ? "http://localhost:3000" : window.location.origin,
  credentials: "same-origin",
  headers: { "X-Pantrio-Csrf": "1" },
});

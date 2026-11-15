import createClient from "openapi-fetch";
import type { components, paths } from "./schema";

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

export type Schemas = components["schemas"];

type ProblemLike = {
  title?: string | null;
  detail?: string | null;
  errors?: Record<string, string[]>;
};

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
    public readonly fieldErrors: Record<string, string[]> = {},
  ) {
    super(message);
    this.name = "ApiError";
  }
}

/** Turns an openapi-fetch result into data or a thrown ApiError with a message people can read. */
export async function unwrap<T>(
  call: Promise<{ data?: T; error?: unknown; response: Response }>,
): Promise<T> {
  const { data, error, response } = await call;
  if (response.ok) {
    return data as T;
  }

  const problem = (error ?? {}) as ProblemLike;
  const fieldMessage = problem.errors ? Object.values(problem.errors).flat()[0] : undefined;
  const message =
    problem.detail ??
    fieldMessage ??
    problem.title ??
    (response.status === 401 ? "Please sign in again." : `Something went wrong (${response.status}).`);
  throw new ApiError(response.status, message, problem.errors);
}

export function errorMessage(err: unknown): string {
  if (err instanceof ApiError) return err.message;
  if (err instanceof Error) return err.message;
  return "Something went wrong.";
}

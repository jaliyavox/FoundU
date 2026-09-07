import { tokenStore } from './tokens'
import type { AuthResponse, ProblemDetails } from './types'

const BASE_URL = import.meta.env.VITE_API_BASE_URL as string

/** Thrown for every non-2xx response, carrying the ProblemDetails envelope from the API. */
export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails

  constructor(status: number, problem: ProblemDetails) {
    super(problem.detail || problem.title || `Request failed with status ${status}`)
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }

  /** Field-level validation messages; empty for anything other than a 400. */
  get fieldErrors(): Record<string, string[]> {
    return this.problem.errors ?? {}
  }
}

/** Registered by AuthProvider so this module can end a session without importing the router. */
let onSessionExpired: (() => void) | null = null

export function setOnSessionExpired(handler: () => void) {
  onSessionExpired = handler
}

/**
 * Shared across all callers so concurrent 401s trigger exactly one refresh.
 *
 * The API rotates refresh tokens: /api/auth/refresh invalidates the token it was given and
 * issues a new pair. If four parallel requests each refreshed independently, the first would
 * win and the other three would be left holding a dead token - logging the user out mid-session.
 */
let refreshPromise: Promise<string | null> | null = null

function refreshAccessToken(): Promise<string | null> {
  refreshPromise ??= performRefresh().finally(() => {
    refreshPromise = null
  })
  return refreshPromise
}

async function performRefresh(): Promise<string | null> {
  const refreshToken = tokenStore.getRefreshToken()
  if (!refreshToken) return null

  const response = await fetch(`${BASE_URL}/api/auth/refresh`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ refreshToken }),
  })

  if (!response.ok) {
    tokenStore.clear()
    return null
  }

  const auth = (await response.json()) as AuthResponse
  tokenStore.save(auth)
  return auth.accessToken
}

/**
 * Absolute URL for a file the API serves out of its own wwwroot. Upload URLs come back
 * host-relative ("/uploads/..."), which in dev would resolve against the Vite origin on
 * 5173 rather than the API, and 404.
 */
export const assetUrl = (path: string) => (path.startsWith('http') ? path : `${BASE_URL}${path}`)

export interface RequestOptions {
  method?: string
  body?: unknown
  /** Skip the bearer token and the 401-refresh retry - for login, register and refresh itself. */
  anonymous?: boolean
  /**
   * Send the token when there is one, but never refresh or expire the session over it. For
   * endpoints that work signed out and only say a little more to someone signed in: an
   * expired token there should degrade to anonymous, not sign the reader out mid-browse.
   */
  optionalAuth?: boolean
  signal?: AbortSignal
}

function send(path: string, options: RequestOptions, token: string | null) {
  const headers: Record<string, string> = {}

  if (options.body !== undefined) {
    headers['Content-Type'] = 'application/json'
  }

  if (token && !options.anonymous) {
    headers.Authorization = `Bearer ${token}`
  }

  return fetch(`${BASE_URL}${path}`, {
    method: options.method ?? 'GET',
    headers,
    body: options.body === undefined ? undefined : JSON.stringify(options.body),
    signal: options.signal,
  })
}

async function toApiError(response: Response): Promise<ApiError> {
  try {
    const problem = (await response.json()) as ProblemDetails
    return new ApiError(response.status, problem)
  } catch {
    // A crash behind a proxy, or a network-level failure, may not return JSON at all.
    return new ApiError(response.status, {
      title: response.statusText || 'Request failed',
      status: response.status,
    })
  }
}

export async function apiFetch<T>(path: string, options: RequestOptions = {}): Promise<T> {
  let response = await send(path, options, tokenStore.getAccessToken())

  if (response.status === 401 && !options.anonymous && !options.optionalAuth) {
    const freshToken = await refreshAccessToken()

    if (!freshToken) {
      tokenStore.clear()
      onSessionExpired?.()
      throw await toApiError(response)
    }

    // Retried exactly once. A second 401 means the new token is genuinely rejected,
    // so the error surfaces instead of looping.
    response = await send(path, options, freshToken)
  }

  if (!response.ok) {
    throw await toApiError(response)
  }

  // 204 No Content (logout, for example) has no body to parse.
  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}

export const api = {
  get: <T>(path: string, options?: RequestOptions) =>
    apiFetch<T>(path, { ...options, method: 'GET' }),

  post: <T>(path: string, body?: unknown, options?: RequestOptions) =>
    apiFetch<T>(path, { ...options, method: 'POST', body }),

  put: <T>(path: string, body?: unknown, options?: RequestOptions) =>
    apiFetch<T>(path, { ...options, method: 'PUT', body }),

  delete: <T>(path: string, options?: RequestOptions) =>
    apiFetch<T>(path, { ...options, method: 'DELETE' }),
}

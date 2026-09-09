import { tokenStore } from './tokens'
import type { AuthResponse, AuthUser, ProblemDetails } from './types'

const BASE_URL = (import.meta.env.VITE_API_BASE_URL as string | undefined)?.trim().replace(/\/+$/, '')

function apiUrl(path: string) {
  if (!BASE_URL) {
    throw new Error('Set VITE_API_BASE_URL in web/.env.local using web/.env.example, then restart Vite.')
  }
  return `${BASE_URL}${path}`
}

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

/** The provider owns React state/cache; the client owns request/session sequencing. */
let onSessionChanged: ((user: AuthUser | null) => void) | null = null
let sessionVersion = 0

export function setOnSessionChanged(handler: typeof onSessionChanged) {
  onSessionChanged = handler
}

export function getSessionVersion() {
  return sessionVersion
}

function assertCurrentSession(version: number) {
  if (version !== sessionVersion) {
    throw new DOMException('The authentication session changed.', 'AbortError')
  }
}

export function clearSession() {
  sessionVersion++
  refreshPromise = null
  tokenStore.clear()
  onSessionChanged?.(null)
}

/** Used after login/register; an older response cannot replace a newer session. */
export function startSession(auth: AuthResponse, version: number) {
  assertCurrentSession(version)
  sessionVersion++
  tokenStore.save(auth)
  onSessionChanged?.(auth.user)
}

/**
 * Shared across all callers so concurrent 401s trigger exactly one refresh.
 *
 * The API rotates refresh tokens: /api/auth/refresh invalidates the token it was given and
 * issues a new pair. If four parallel requests each refreshed independently, the first would
 * win and the other three would be left holding a dead token - logging the user out mid-session.
 */
let refreshPromise: Promise<string | null> | null = null

function refreshAccessToken(version: number): Promise<string | null> {
  assertCurrentSession(version)
  if (!refreshPromise) {
    const pending = performRefresh(version).finally(() => {
      if (refreshPromise === pending) refreshPromise = null
    })
    refreshPromise = pending
  }
  return refreshPromise
}

async function performRefresh(version: number): Promise<string | null> {
  const refreshToken = tokenStore.getRefreshToken()
  if (!refreshToken) return null

  const response = await fetch(apiUrl('/api/auth/refresh'), {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ refreshToken }),
  })
  assertCurrentSession(version)

  if (!response.ok) {
    if (response.status === 400 || response.status === 401 || response.status === 403) {
      clearSession()
      return null
    }
    throw await toApiError(response)
  }

  const auth = (await response.json()) as AuthResponse
  assertCurrentSession(version)
  tokenStore.save(auth)
  onSessionChanged?.(auth.user)
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
  /** Plain values are JSON encoded; FormData is sent as multipart with its browser boundary. */
  body?: unknown | FormData
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
  const body = options.body

  if (body !== undefined && !(body instanceof FormData)) {
    headers['Content-Type'] = 'application/json'
  }

  if (token && !options.anonymous) {
    headers.Authorization = `Bearer ${token}`
  }

  return fetch(apiUrl(path), {
    method: options.method ?? 'GET',
    headers,
    body: body === undefined
      ? undefined
      : body instanceof FormData
        ? body
        : JSON.stringify(body),
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
  const version = sessionVersion
  const originalToken = tokenStore.getAccessToken()
  let response = await send(path, options, originalToken)
  if (!options.anonymous) assertCurrentSession(version)

  if (response.status === 401 && !options.anonymous && !options.optionalAuth) {
    // A late 401 may arrive after another request already finished rotating tokens.
    const currentToken = tokenStore.getAccessToken()
    const freshToken = currentToken && currentToken !== originalToken
      ? currentToken
      : await refreshAccessToken(version)

    if (!freshToken) {
      if (version === sessionVersion) clearSession()
      throw await toApiError(response)
    }
    assertCurrentSession(version)

    // Retried exactly once. A second 401 means the new token is genuinely rejected,
    // so the error surfaces instead of looping.
    response = await send(path, options, freshToken)
    assertCurrentSession(version)
    if (response.status === 401) clearSession()
  }

  if (!response.ok) {
    throw await toApiError(response)
  }

  // 204 No Content (logout, for example) has no body to parse.
  if (response.status === 204) {
    return undefined as T
  }

  const result = (await response.json()) as T
  if (!options.anonymous) assertCurrentSession(version)
  return result
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

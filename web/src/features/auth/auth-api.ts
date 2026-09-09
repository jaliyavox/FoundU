import { api } from '@/lib/api/client'
import type { AuthResponse, AuthUser, MeResponse } from '@/lib/api/types'

/**
 * These three are anonymous: login and register have no token yet, and logout deliberately
 * sends the refresh token in the body rather than relying on the access token, so it still
 * works when the access token has already expired.
 */

export function login(email: string, password: string) {
  return api.post<AuthResponse>('/api/auth/login', { email, password }, { anonymous: true })
}

export function register(fullName: string, email: string, password: string, studentNumber?: string) {
  return api.post<AuthResponse>(
    '/api/auth/register',
    { fullName, email, password, studentNumber },
    { anonymous: true },
  )
}

export function logout(refreshToken: string) {
  return api.post<void>('/api/auth/logout', { refreshToken }, { anonymous: true })
}

/** Authenticated: proves the stored access token is still valid. */
export function me() {
  return api.get<MeResponse>('/api/auth/me')
}

export function authUserFromMe(profile: MeResponse): AuthUser {
  return { id: profile.id, email: profile.email, fullName: profile.name, role: profile.role }
}

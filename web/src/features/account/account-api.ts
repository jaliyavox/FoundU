import { api } from '@/lib/api/client'
import type { AuthResponse } from '@/lib/api/types'

/** Mirrors FoundU.Application.Auth.Dtos.ProfileDto. */
export interface Profile {
  id: string
  fullName: string
  email: string
  role: string
  studentNumber: string | null
  /** False for an account created through Google that has never set one. */
  hasPassword: boolean
  isGoogleLinked: boolean
  createdAt: string
}

export interface UpdateProfileInput {
  fullName: string
  email: string
  studentNumber?: string | null
  /** Required when the email is changing and the account has a password. */
  currentPassword?: string
}

export const getProfile = () => api.get<Profile>('/api/profile')

export const updateProfile = (input: UpdateProfileInput) => api.put<Profile>('/api/profile', input)

/**
 * Returns a fresh token pair: the change signs out every other session, and this one would
 * go with them otherwise. The caller must store them.
 */
export const changePassword = (input: { currentPassword?: string; newPassword: string }) =>
  api.post<AuthResponse>('/api/profile/password', input)

/* --------------------------------------------------------------- google sign-in */

export const googleSignIn = (idToken: string) =>
  api.post<AuthResponse>('/api/auth/google', { idToken })

/** Whether this server has a Google client id, so the button can stay hidden when it does not. */
/** Whether Google sign-in is on, and the client id the API checks tokens against. */
export const getGoogleStatus = () =>
  api.get<{ enabled: boolean; clientId: string | null }>('/api/auth/google/status')

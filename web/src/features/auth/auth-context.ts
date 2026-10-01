import { createContext } from 'react'
import type { AuthResponse, AuthUser } from '@/lib/api/types'

export interface RegisterInput {
  fullName: string
  email: string
  password: string
  studentNumber?: string
}

export interface AuthContextValue {
  user: AuthUser | null
  isInitializing: boolean
  isAuthenticated: boolean
  login: (email: string, password: string) => Promise<AuthUser>
  register: (input: RegisterInput) => Promise<AuthUser>
  logout: () => Promise<void>
  /** Signs in with the ID token Google's button produced. */
  signInWithGoogle: (idToken: string) => Promise<AuthUser>
  /** Stores a token pair the app already holds - a password change hands one back. */
  applySession: (auth: AuthResponse) => AuthUser
  /**
   * Updates the signed-in user shown around the app after they edit their own profile. The
   * access token still carries the old name until it is next refreshed, so the name on screen
   * comes from here rather than from the claims.
   */
  updateLocalUser: (patch: Partial<AuthUser>) => void
}

/**
 * Null until an AuthProvider is mounted; useAuth turns that into a clear error rather
 * than letting components read undefined values.
 */
export const AuthContext = createContext<AuthContextValue | null>(null)

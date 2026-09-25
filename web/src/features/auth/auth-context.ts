import { createContext } from 'react'
import type { AuthUser } from '@/lib/api/types'

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
}

/**
 * Null until an AuthProvider is mounted; useAuth turns that into a clear error rather
 * than letting components read undefined values.
 */
export const AuthContext = createContext<AuthContextValue | null>(null)

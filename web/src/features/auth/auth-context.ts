import { createContext } from 'react'
import type { User } from '@/lib/api/types'

export interface AuthContextValue {
  user: User | null
  isAuthenticated: boolean
  login: (email: string, password: string) => Promise<User>
  logout: () => Promise<void>
}

/**
 * Null until an AuthProvider is mounted; useAuth turns that into a clear error rather
 * than letting components read undefined values.
 */
export const AuthContext = createContext<AuthContextValue | null>(null)

import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { AuthContext, type AuthContextValue, type RegisterInput } from './auth-context'
import * as authApi from './auth-api'
import { setOnSessionExpired } from '@/lib/api/client'
import { tokenStore } from '@/lib/api/tokens'
import type { User } from '@/lib/api/types'

export function AuthProvider({ children }: { children: ReactNode }) {
  // Seeded from storage so a reload renders the app immediately rather than
  // flashing the login page while a network round-trip confirms the session.
  const [user, setUser] = useState<User | null>(() => tokenStore.getUser())

  useEffect(() => {
    // The API client calls this when a refresh fails, so an expired session clears
    // React state too instead of leaving a stale user rendered in the UI.
    setOnSessionExpired(() => setUser(null))
  }, [])

  const login = useCallback(async (email: string, password: string) => {
    const auth = await authApi.login(email, password)
    tokenStore.save(auth)
    setUser(auth.user)
    return auth.user
  }, [])

  const register = useCallback(async (input: RegisterInput) => {
    // The API signs the new student straight in, so there is no second login round-trip.
    const auth = await authApi.register(
      input.fullName,
      input.email,
      input.password,
      input.studentNumber,
    )
    tokenStore.save(auth)
    setUser(auth.user)
    return auth.user
  }, [])

  const logout = useCallback(async () => {
    const refreshToken = tokenStore.getRefreshToken()

    if (refreshToken) {
      try {
        await authApi.logout(refreshToken)
      } catch {
        // Best-effort: a failed revoke on the server must not trap the user in the app.
      }
    }

    tokenStore.clear()
    setUser(null)
  }, [])

  const value = useMemo<AuthContextValue>(
    () => ({ user, isAuthenticated: user !== null, login, register, logout }),
    [user, login, register, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

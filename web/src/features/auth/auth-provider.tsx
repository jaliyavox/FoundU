import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { AuthContext, type AuthContextValue, type RegisterInput } from './auth-context'
import * as authApi from './auth-api'
import { clearSession, getSessionVersion, setOnSessionChanged, startSession } from '@/lib/api/client'
import { tokenStore } from '@/lib/api/tokens'
import type { AuthUser, MeResponse } from '@/lib/api/types'

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const [user, setUser] = useState<AuthUser | null>(null)
  const [isInitializing, setIsInitializing] = useState(true)
  const initializing = useRef(true)
  // Reuse the startup request when StrictMode replays the effect in development.
  const initialization = useRef<{ version: number; promise: Promise<MeResponse> } | null>(null)

  useEffect(() => {
    let active = true
    setOnSessionChanged((nextUser) => {
      if (!nextUser) {
        // Private query keys are not scoped to user IDs. Clear the shared client,
        // including pending queries, before another account can reuse its data.
        queryClient.clear()
        initializing.current = false
        setIsInitializing(false)
        setUser(null)
      } else if (!initializing.current) {
        // Refresh responses are authoritative too; startup still waits for /me.
        setUser(nextUser)
      }
    })

    const version = getSessionVersion()
    if (!tokenStore.getAccessToken() && !tokenStore.getRefreshToken()) {
      clearSession()
    } else {
      if (initialization.current?.version !== version) {
        initialization.current = { version, promise: authApi.me() }
      }
      void initialization.current.promise.then((profile) => {
        if (!active || version !== getSessionVersion()) return
        setUser(authApi.authUserFromMe(profile))
        initializing.current = false
        setIsInitializing(false)
      }).catch(() => {
        if (active && version === getSessionVersion()) {
          // Fail closed even if startup validation cannot reach the API.
          clearSession()
        }
      })
    }

    return () => {
      active = false
      setOnSessionChanged(null)
    }
  }, [queryClient])

  const login = useCallback(async (email: string, password: string) => {
    clearSession()
    const version = getSessionVersion()
    const auth = await authApi.login(email, password)
    startSession(auth, version)
    return auth.user
  }, [])

  const register = useCallback(async (input: RegisterInput) => {
    clearSession()
    const version = getSessionVersion()
    // The API signs the new student straight in, so there is no second login round-trip.
    const auth = await authApi.register(
      input.fullName,
      input.email,
      input.password,
      input.studentNumber,
    )
    startSession(auth, version)
    return auth.user
  }, [])

  const logout = useCallback(async () => {
    const refreshToken = tokenStore.getRefreshToken()
    // Invalidate older requests and clear React/storage/cache before a network wait.
    clearSession()

    if (refreshToken) {
      try {
        await authApi.logout(refreshToken)
      } catch {
        // Revocation is best-effort; local logout has already completed.
      }
    }

  }, [])

  const value = useMemo<AuthContextValue>(
    () => ({ user, isInitializing, isAuthenticated: user !== null && !isInitializing, login, register, logout }),
    [user, isInitializing, login, register, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

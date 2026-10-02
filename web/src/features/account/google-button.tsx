import { useEffect, useRef, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { toast } from 'sonner'
import { useAuth } from '@/features/auth/use-auth'
import { homeRouteForRole } from '@/routes/role-home'
import { ApiError } from '@/lib/api/client'
import { getGoogleStatus } from './account-api'

/** Google Identity Services, loaded only when there is a client id to use it with. */
const GSI_SRC = 'https://accounts.google.com/gsi/client'

declare global {
  interface Window {
    google?: {
      accounts: {
        id: {
          initialize: (config: { client_id: string; callback: (response: { credential?: string }) => void }) => void
          renderButton: (parent: HTMLElement, options: Record<string, unknown>) => void
        }
      }
    }
  }
}

/**
 * "Continue with Google". Renders nothing unless the API has a client id - it hands the
 * button that same id - because a button that cannot work is worse than no button.
 *
 * The browser only ever hands us an ID token; the API verifies it against Google's keys
 * before anybody is signed in.
 */
export function GoogleButton({ label = 'signin_with' }: { label?: 'signin_with' | 'signup_with' }) {
  const { signInWithGoogle } = useAuth()
  const navigate = useNavigate()
  const holder = useRef<HTMLDivElement>(null)
  const [scriptReady, setScriptReady] = useState(false)

  // The API is the one place the client id is configured: the button uses the same id the
  // tokens are checked against, so the two can never disagree.
  const { data } = useQuery({
    queryKey: ['google-status'],
    queryFn: getGoogleStatus,
    retry: false,
    staleTime: 5 * 60 * 1000,
  })

  const clientId = data?.enabled ? data.clientId ?? '' : ''
  const enabled = clientId.length > 0

  useEffect(() => {
    if (!enabled || window.google) {
      if (window.google) setScriptReady(true)
      return
    }

    const existing = document.querySelector<HTMLScriptElement>(`script[src="${GSI_SRC}"]`)
    if (existing) {
      existing.addEventListener('load', () => setScriptReady(true), { once: true })
      return
    }

    const script = document.createElement('script')
    script.src = GSI_SRC
    script.async = true
    script.defer = true
    script.addEventListener('load', () => setScriptReady(true), { once: true })
    document.head.append(script)
  }, [enabled])

  useEffect(() => {
    if (!enabled || !scriptReady || !holder.current || !window.google) return

    window.google.accounts.id.initialize({
      client_id: clientId,
      callback: async response => {
        if (!response.credential) return
        try {
          const user = await signInWithGoogle(response.credential)
          navigate(homeRouteForRole(user.role), { replace: true })
        } catch (error) {
          toast.error(
            error instanceof ApiError ? error.message : 'Could not sign in with Google.',
          )
        }
      },
    })

    window.google.accounts.id.renderButton(holder.current, {
      type: 'standard',
      theme: 'outline',
      size: 'large',
      text: label,
      shape: 'pill',
      width: 320,
      // Google otherwise follows the computer's language, which put Sinhala on an English page.
      locale: 'en',
    })
  }, [enabled, clientId, scriptReady, label, navigate, signInWithGoogle])

  if (!enabled) return null

  return (
    <div className="flex flex-col gap-4">
      <div ref={holder} className="flex justify-center" />
      <div className="flex items-center gap-3">
        <span className="h-px flex-1 bg-foreground/15" />
        <span className="text-xs text-muted-foreground">or</span>
        <span className="h-px flex-1 bg-foreground/15" />
      </div>
    </div>
  )
}

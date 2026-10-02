import { useEffect, useRef, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { CheckCircle2Icon, Loader2Icon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { useAuth } from './use-auth'
import { AuthLayout } from './auth-layout'
import { confirmEmail } from './account-email-api'

/** Opened from the confirmation email. Confirms on arrival - the click was the decision. */
const backToSignIn = (
  <Link to="/login" className="font-medium text-primary underline-offset-4 hover:underline">
    Back to sign in
  </Link>
)

export function ConfirmEmailPage() {
  const { user } = useAuth()
  const [params] = useSearchParams()
  const userId = params.get('userId') ?? ''
  const token = params.get('token') ?? ''
  const [state, setState] = useState<'working' | 'done' | 'failed'>(userId && token ? 'working' : 'failed')

  // Scheduled and cancelled on cleanup, so React's development double mount sends it once.
  const sent = useRef(false)
  useEffect(() => {
    if (!userId || !token || sent.current) return
    const timer = setTimeout(() => {
      sent.current = true
      confirmEmail(userId, token)
        .then(() => setState('done'))
        .catch(() => setState('failed'))
    }, 0)
    return () => clearTimeout(timer)
  }, [userId, token])

  const next = user ? { to: '/account', label: 'Back to your account' } : { to: '/login', label: 'Sign in' }

  if (state === 'working') {
    return (
      <AuthLayout footer={backToSignIn} title="Confirming your email" subtitle="One moment.">
        <Loader2Icon className="size-6 animate-spin text-muted-foreground" aria-label="Confirming" />
      </AuthLayout>
    )
  }

  if (state === 'done') {
    return (
      <AuthLayout footer={backToSignIn} title="Email confirmed" subtitle="Thanks - we can reach you when something of yours turns up, and you can reset your password if you ever need to.">
        <div className="flex flex-col gap-4">
          <p className="flex items-center gap-2 text-sm text-brand-green">
            <CheckCircle2Icon className="size-4" aria-hidden="true" />
            Confirmed
          </p>
          <Button size="lg" nativeButton={false} render={<Link to={next.to} />}>
            {next.label}
          </Button>
        </div>
      </AuthLayout>
    )
  }

  return (
    <AuthLayout footer={backToSignIn}
      title="That link has run out"
      subtitle="Confirmation links work once and expire. Sign in and use Send the link again on your account page."
    >
      <Button size="lg" nativeButton={false} render={<Link to={next.to} />}>
        {next.label}
      </Button>
    </AuthLayout>
  )
}

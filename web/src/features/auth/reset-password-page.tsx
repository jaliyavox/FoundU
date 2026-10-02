import { useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { CheckCircle2Icon, EyeIcon, EyeOffIcon, Loader2Icon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { ApiError } from '@/lib/api/client'
import { AuthLayout } from './auth-layout'
import { resetPassword } from './account-email-api'

/** Opened from the reset email. The link carries who and a one-time token. */
const backToSignIn = (
  <Link to="/login" className="font-medium text-primary underline-offset-4 hover:underline">
    Back to sign in
  </Link>
)

export function ResetPasswordPage() {
  const [params] = useSearchParams()
  const email = params.get('email') ?? ''
  const token = params.get('token') ?? ''
  const [password, setPassword] = useState('')
  const [repeat, setRepeat] = useState('')
  const [show, setShow] = useState(false)
  const [busy, setBusy] = useState(false)
  const [done, setDone] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [expired, setExpired] = useState(!email || !token)

  const mismatch = repeat.length > 0 && repeat !== password

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (busy || mismatch || !password) return
    setBusy(true)
    setError(null)
    try {
      await resetPassword(email, token, password)
      setDone(true)
    } catch (caught) {
      if (caught instanceof ApiError) {
        if (caught.fieldErrors.Token) setExpired(true)
        else setError(Object.values(caught.fieldErrors).flat().join(' ') || caught.message)
      } else {
        setError('Could not reach FoundU. Try again in a moment.')
      }
    } finally {
      setBusy(false)
    }
  }

  if (expired) {
    return (
      <AuthLayout footer={backToSignIn} title="That link has run out" subtitle="Reset links work once and expire after a day. Ask for a fresh one - it only takes a moment.">
        <Button size="lg" nativeButton={false} render={<Link to="/forgot-password" />}>
          Send me a new link
        </Button>
      </AuthLayout>
    )
  }

  if (done) {
    return (
      <AuthLayout footer={backToSignIn} title="Password changed" subtitle="Sign in with your new password. Anywhere else you were signed in has been signed out.">
        <div className="flex flex-col gap-4">
          <p className="flex items-center gap-2 text-sm text-brand-green">
            <CheckCircle2Icon className="size-4" aria-hidden="true" />
            Your email is confirmed too.
          </p>
          <Button size="lg" nativeButton={false} render={<Link to="/login" />}>
            Sign in
          </Button>
        </div>
      </AuthLayout>
    )
  }

  return (
    <AuthLayout footer={backToSignIn} title="Choose a new password" subtitle={`For ${email}. At least 8 characters, with an upper-case letter, a lower-case letter and a number.`}>
      <form onSubmit={submit} noValidate className="flex flex-col gap-5">
        <div className="flex flex-col gap-2">
          <Label htmlFor="new-password">New password</Label>
          <div className="relative">
            <Input
              id="new-password"
              type={show ? 'text' : 'password'}
              autoComplete="new-password"
              required
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              className="pr-10"
            />
            <button
              type="button"
              onClick={() => setShow((value) => !value)}
              aria-label={show ? 'Hide password' : 'Show password'}
              className="absolute top-1/2 right-2 flex size-7 -translate-y-1/2 items-center justify-center rounded-md text-muted-foreground hover:text-foreground"
            >
              {show ? <EyeOffIcon className="size-4" /> : <EyeIcon className="size-4" />}
            </button>
          </div>
        </div>
        <div className="flex flex-col gap-2">
          <Label htmlFor="repeat-password">Type it again</Label>
          <Input
            id="repeat-password"
            type={show ? 'text' : 'password'}
            autoComplete="new-password"
            required
            value={repeat}
            onChange={(event) => setRepeat(event.target.value)}
            aria-invalid={mismatch}
          />
          {mismatch && <p className="text-sm text-destructive">The two don&apos;t match yet.</p>}
          {error && (
            <p role="alert" className="text-sm text-destructive">
              {error}
            </p>
          )}
        </div>
        <Button type="submit" size="lg" disabled={busy || !password || mismatch || repeat !== password} className="w-full">
          {busy && <Loader2Icon className="animate-spin" aria-hidden="true" />}
          Save new password
        </Button>
      </form>
    </AuthLayout>
  )
}

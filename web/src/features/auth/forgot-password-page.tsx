import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { Loader2Icon, MailCheckIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { ApiError } from '@/lib/api/client'
import { AuthLayout } from './auth-layout'
import { requestPasswordReset } from './account-email-api'

/**
 * "Forgot password". The answer is the same whether or not the address has an account, so
 * the form cannot be used to find out who is signed up.
 */
export function ForgotPasswordPage() {
  const [email, setEmail] = useState('')
  const [sent, setSent] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (!email.trim() || busy) return
    setBusy(true)
    setError(null)
    try {
      await requestPasswordReset(email.trim())
      setSent(true)
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'Could not reach FoundU. Try again in a moment.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <AuthLayout
      title={sent ? 'Check your inbox' : 'Forgot your password?'}
      subtitle={
        sent
          ? `If ${email.trim()} has a FoundU account, a link to choose a new password is on its way. It works once and expires in a day.`
          : 'Enter the email you signed up with and we will send you a link to choose a new one.'
      }
      footer={
        <>
          Remembered it?{' '}
          <Link to="/login" className="font-medium text-primary underline-offset-4 hover:underline">
            Back to sign in
          </Link>
        </>
      }
    >
      {sent ? (
        <div className="flex flex-col gap-4">
          <div className="flex items-start gap-3 rounded-xl border border-brand-green/25 bg-brand-green/8 p-4 text-sm">
            <MailCheckIcon className="mt-0.5 size-5 shrink-0 text-brand-green" aria-hidden="true" />
            <p>
              It comes from <strong>noreply@thejaliya.com</strong>. Nothing after a few minutes? Check your spam
              folder, then try again.
            </p>
          </div>
          <Button variant="outline" onClick={() => setSent(false)}>
            Use a different email
          </Button>
        </div>
      ) : (
        <form onSubmit={submit} noValidate className="flex flex-col gap-5">
          <div className="flex flex-col gap-2">
            <Label htmlFor="email">Email address</Label>
            <Input
              id="email"
              type="email"
              autoComplete="email"
              placeholder="you@campus.edu"
              required
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              aria-invalid={Boolean(error)}
              aria-describedby={error ? 'forgot-error' : undefined}
            />
            {error && (
              <p id="forgot-error" role="alert" className="text-sm text-destructive">
                {error}
              </p>
            )}
          </div>
          <Button type="submit" size="lg" disabled={busy || !email.trim()} className="w-full">
            {busy && <Loader2Icon className="animate-spin" aria-hidden="true" />}
            Send the reset link
          </Button>
        </form>
      )}
    </AuthLayout>
  )
}

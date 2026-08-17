import { useState, type FormEvent } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import { Loader2Icon } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useAuth } from './use-auth'
import { ApiError } from '@/lib/api/client'
import { homeRouteForRole } from '@/routes/role-home'

export function LoginPage() {
  const { user, login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({})

  // Already signed in - skip the form entirely.
  if (user) {
    return <Navigate to={homeRouteForRole(user.role)} replace />
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setIsSubmitting(true)
    setFieldErrors({})

    try {
      const signedIn = await login(email, password)

      // Return them to wherever the guard interrupted, otherwise their role's home.
      const from = (location.state as { from?: { pathname: string } } | null)?.from?.pathname
      navigate(from ?? homeRouteForRole(signedIn.role), { replace: true })
    } catch (error) {
      if (error instanceof ApiError) {
        setFieldErrors(error.fieldErrors)
        // 401 from login means bad credentials; anything else is worth showing verbatim.
        toast.error(error.status === 401 ? 'Incorrect email or password.' : error.message)
      } else {
        toast.error('Could not reach the server. Is the API running?')
      }
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <main className="flex min-h-svh items-center justify-center px-4 py-10">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle>Sign in to FoundU</CardTitle>
          <CardDescription>Campus lost &amp; found</CardDescription>
        </CardHeader>

        <CardContent>
          <form onSubmit={handleSubmit} noValidate className="flex flex-col gap-4">
            <div className="flex flex-col gap-2">
              <Label htmlFor="email">Email</Label>
              <Input
                id="email"
                name="email"
                type="email"
                autoComplete="email"
                required
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                aria-invalid={Boolean(fieldErrors.Email)}
                aria-describedby={fieldErrors.Email ? 'email-error' : undefined}
              />
              {fieldErrors.Email && (
                <p id="email-error" className="text-sm text-destructive">
                  {fieldErrors.Email.join(' ')}
                </p>
              )}
            </div>

            <div className="flex flex-col gap-2">
              <Label htmlFor="password">Password</Label>
              <Input
                id="password"
                name="password"
                type="password"
                autoComplete="current-password"
                required
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                aria-invalid={Boolean(fieldErrors.Password)}
                aria-describedby={fieldErrors.Password ? 'password-error' : undefined}
              />
              {fieldErrors.Password && (
                <p id="password-error" className="text-sm text-destructive">
                  {fieldErrors.Password.join(' ')}
                </p>
              )}
            </div>

            <Button type="submit" disabled={isSubmitting} className="mt-2">
              {isSubmitting && <Loader2Icon className="animate-spin" aria-hidden="true" />}
              {isSubmitting ? 'Signing in...' : 'Sign in'}
            </Button>
          </form>
        </CardContent>
      </Card>
    </main>
  )
}

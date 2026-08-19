import { useMemo, useState, type FormEvent } from 'react'
import { Link, Navigate, useNavigate } from 'react-router-dom'
import { CheckIcon, EyeIcon, EyeOffIcon, Loader2Icon } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { AuthLayout } from './auth-layout'
import { useAuth } from './use-auth'
import { ApiError } from '@/lib/api/client'
import { cn } from '@/lib/utils'
import { homeRouteForRole } from '@/routes/role-home'

/** Mirrors RegisterRequestValidator on the API, so the two never disagree. */
const PASSWORD_RULES = [
  { label: 'At least 8 characters', test: (value: string) => value.length >= 8 },
  { label: 'An uppercase letter', test: (value: string) => /[A-Z]/.test(value) },
  { label: 'A lowercase letter', test: (value: string) => /[a-z]/.test(value) },
  { label: 'A number', test: (value: string) => /[0-9]/.test(value) },
]

export function RegisterPage() {
  const { user, register } = useAuth()
  const navigate = useNavigate()

  const [fullName, setFullName] = useState('')
  const [email, setEmail] = useState('')
  const [studentNumber, setStudentNumber] = useState('')
  const [password, setPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({})

  const rules = useMemo(
    () => PASSWORD_RULES.map((rule) => ({ ...rule, passed: rule.test(password) })),
    [password],
  )
  const metCount = rules.filter((rule) => rule.passed).length

  if (user) {
    return <Navigate to={homeRouteForRole(user.role)} replace />
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setIsSubmitting(true)
    setFieldErrors({})

    try {
      const created = await register({
        fullName,
        email,
        password,
        studentNumber: studentNumber.trim() || undefined,
      })
      toast.success(`Welcome to FoundU, ${created.fullName.split(' ')[0]}.`)
      navigate(homeRouteForRole(created.role), { replace: true })
    } catch (error) {
      if (error instanceof ApiError) {
        setFieldErrors(error.fieldErrors)
        // 409 is the "email already registered" case and deserves a clearer nudge.
        toast.error(
          error.status === 409
            ? 'An account with this email already exists. Try signing in instead.'
            : error.message,
        )
      } else {
        toast.error('Could not reach the server. Is the API running?')
      }
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <AuthLayout
      title="Create your FoundU account"
      subtitle="For students. Staff and administrator accounts are created by an administrator."
      footer={
        <>
          Already have an account?{' '}
          <Link
            to="/login"
            className="font-medium text-primary underline-offset-4 hover:underline focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none"
          >
            Sign in
          </Link>
        </>
      }
    >
      <form onSubmit={handleSubmit} noValidate className="flex flex-col gap-5">
        <Field
          id="fullName"
          label="Full name"
          autoComplete="name"
          value={fullName}
          onChange={setFullName}
          errors={fieldErrors.FullName}
          placeholder="Jaliya Perera"
        />

        <Field
          id="email"
          label="Email address"
          type="email"
          autoComplete="email"
          value={email}
          onChange={setEmail}
          errors={fieldErrors.Email}
          placeholder="you@campus.edu"
        />

        <Field
          id="studentNumber"
          label="Student number"
          optional
          autoComplete="off"
          value={studentNumber}
          onChange={setStudentNumber}
          errors={fieldErrors.StudentNumber}
          placeholder="IT21001"
        />

        <div className="flex flex-col gap-2">
          <Label htmlFor="password">Password</Label>
          <div className="relative">
            <Input
              id="password"
              name="password"
              type={showPassword ? 'text' : 'password'}
              autoComplete="new-password"
              required
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              aria-invalid={Boolean(fieldErrors.Password)}
              aria-describedby="password-rules"
              className="pr-10"
            />
            <button
              type="button"
              onClick={() => setShowPassword((shown) => !shown)}
              aria-label={showPassword ? 'Hide password' : 'Show password'}
              className="absolute inset-y-0 right-0 flex w-10 items-center justify-center rounded-r-lg text-muted-foreground transition-colors hover:text-foreground focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none"
            >
              {showPassword ? <EyeOffIcon className="size-4" /> : <EyeIcon className="size-4" />}
            </button>
          </div>

          {/* Strength meter: four segments, one per rule the API actually enforces. */}
          <div className="flex gap-1 pt-1" aria-hidden="true">
            {PASSWORD_RULES.map((rule, index) => (
              <span
                key={rule.label}
                className={cn(
                  'h-1 flex-1 rounded-full transition-colors duration-300',
                  index < metCount ? 'bg-brand-green' : 'bg-muted',
                )}
              />
            ))}
          </div>

          <ul id="password-rules" className="grid gap-1 pt-1 sm:grid-cols-2">
            {rules.map(({ label, passed }) => (
              <li
                key={label}
                className={cn(
                  'flex items-center gap-1.5 text-xs transition-colors duration-200',
                  passed ? 'text-brand-green' : 'text-muted-foreground',
                )}
              >
                <span
                  className={cn(
                    'flex size-3.5 items-center justify-center rounded-full transition-colors duration-200',
                    passed ? 'bg-brand-green text-white' : 'bg-muted',
                  )}
                >
                  {passed && <CheckIcon className="size-2.5" strokeWidth={3.5} />}
                </span>
                {label}
              </li>
            ))}
          </ul>

          {fieldErrors.Password && (
            <p className="text-sm text-destructive">{fieldErrors.Password.join(' ')}</p>
          )}
        </div>

        <Button type="submit" size="lg" disabled={isSubmitting} className="mt-1 w-full">
          {isSubmitting && <Loader2Icon className="animate-spin" aria-hidden="true" />}
          {isSubmitting ? 'Creating your account...' : 'Create account'}
        </Button>
      </form>
    </AuthLayout>
  )
}

function Field({
  id,
  label,
  value,
  onChange,
  errors,
  type = 'text',
  optional = false,
  autoComplete,
  placeholder,
}: {
  id: string
  label: string
  value: string
  onChange: (value: string) => void
  errors?: string[]
  type?: string
  optional?: boolean
  autoComplete?: string
  placeholder?: string
}) {
  return (
    <div className="flex flex-col gap-2">
      <Label htmlFor={id}>
        {label}
        {optional && <span className="pl-1.5 font-normal text-muted-foreground">(optional)</span>}
      </Label>
      <Input
        id={id}
        name={id}
        type={type}
        autoComplete={autoComplete}
        placeholder={placeholder}
        required={!optional}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        aria-invalid={Boolean(errors)}
        aria-describedby={errors ? `${id}-error` : undefined}
      />
      {errors && (
        <p id={`${id}-error`} className="text-sm text-destructive">
          {errors.join(' ')}
        </p>
      )}
    </div>
  )
}

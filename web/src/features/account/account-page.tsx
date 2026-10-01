import { useEffect, useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { KeyRoundIcon, Loader2Icon, MailIcon, ShieldCheckIcon, UserRoundIcon } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { DashboardPanel, PanelDivider } from '@/components/layout/dashboard-panel'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { useAuth } from '@/features/auth/use-auth'
import { ApiError } from '@/lib/api/client'
import { changePassword, getProfile, updateProfile, type Profile } from './account-api'

/**
 * Your own account: name, email, student number and password.
 *
 * Role is shown but not editable - nobody promotes themselves - and the two forms are kept
 * apart because they have different stakes: one is a correction, the other ends every other
 * session you have open.
 */
export function AccountPage() {
  const { data, isPending, isError, refetch } = useQuery({
    queryKey: ['profile'],
    queryFn: getProfile,
  })

  return (
    <section className="mx-auto flex w-full max-w-2xl flex-col gap-6">
      <div>
        <p className="text-sm font-medium text-brand-green">Your account</p>
        <h1 className="pt-1 text-2xl font-semibold tracking-tight">Profile and sign-in</h1>
        <p className="max-w-xl pt-2 text-sm text-muted-foreground">
          Change how your name appears to finders, the address you sign in with, and your
          password.
        </p>
      </div>

      {isPending ? (
        <>
          <Skeleton className="h-64 w-full" />
          <Skeleton className="h-52 w-full" />
        </>
      ) : isError ? (
        <DashboardPanel className="flex flex-col items-start gap-3" role="alert">
          <p className="text-sm">Could not load your account.</p>
          <Button variant="outline" size="sm" onClick={() => refetch()}>Try again</Button>
        </DashboardPanel>
      ) : (
        <>
          <DetailsForm profile={data} />
          <PasswordForm profile={data} />
        </>
      )}
    </section>
  )
}

function DetailsForm({ profile }: { profile: Profile }) {
  const queryClient = useQueryClient()
  const { updateLocalUser } = useAuth()
  const [fullName, setFullName] = useState(profile.fullName)
  const [email, setEmail] = useState(profile.email)
  const [studentNumber, setStudentNumber] = useState(profile.studentNumber ?? '')
  const [currentPassword, setCurrentPassword] = useState('')
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({})

  // A save elsewhere (or a refetch) should not be overwritten by stale form state.
  useEffect(() => {
    setFullName(profile.fullName)
    setEmail(profile.email)
    setStudentNumber(profile.studentNumber ?? '')
  }, [profile])

  const emailChanged = email.trim().toLowerCase() !== profile.email.toLowerCase()
  const needsPassword = emailChanged && profile.hasPassword

  const save = useMutation({
    mutationFn: () =>
      updateProfile({
        fullName: fullName.trim(),
        email: email.trim(),
        studentNumber: studentNumber.trim() || null,
        currentPassword: needsPassword ? currentPassword : undefined,
      }),
    onSuccess: saved => {
      setFieldErrors({})
      setCurrentPassword('')
      queryClient.setQueryData(['profile'], saved)
      // The name in the sidebar comes from the signed-in user, not from this query.
      updateLocalUser({ fullName: saved.fullName, email: saved.email })
      toast.success('Saved.')
    },
    onError: error => {
      if (error instanceof ApiError) {
        setFieldErrors(error.fieldErrors ?? {})
        toast.error(error.message)
      } else {
        toast.error('Could not save your details.')
      }
    },
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    if (!save.isPending) save.mutate()
  }

  return (
    <DashboardPanel>
      <form onSubmit={submit} className="flex flex-col gap-5">
        <div className="flex items-center gap-2">
          <UserRoundIcon className="size-4 text-muted-foreground" aria-hidden="true" />
          <h2 className="font-heading text-base font-medium">Your details</h2>
          <span className="ml-auto text-xs text-muted-foreground">{profile.role}</span>
        </div>

        <PanelDivider />

        <Field id="account-name" label="Full name" errors={fieldErrors.FullName}>
          <Input
            id="account-name"
            value={fullName}
            onChange={event => setFullName(event.target.value)}
            maxLength={120}
            required
            autoComplete="name"
          />
          <p className="text-xs text-muted-foreground">Finders see this on your reports.</p>
        </Field>

        <Field id="account-email" label="Email" errors={fieldErrors.Email}>
          <Input
            id="account-email"
            type="email"
            value={email}
            onChange={event => setEmail(event.target.value)}
            maxLength={256}
            required
            autoComplete="email"
          />
          <p className="text-xs text-muted-foreground">
            This is what you sign in with. It is never shown on the feed.
          </p>
        </Field>

        <Field id="account-student-number" label="Student number (optional)" errors={fieldErrors.StudentNumber}>
          <Input
            id="account-student-number"
            value={studentNumber}
            onChange={event => setStudentNumber(event.target.value)}
            maxLength={40}
          />
        </Field>

        {needsPassword && (
          <Field
            id="account-current-password"
            label="Current password"
            errors={fieldErrors.CurrentPassword}
          >
            <Input
              id="account-current-password"
              type="password"
              value={currentPassword}
              onChange={event => setCurrentPassword(event.target.value)}
              required
              autoComplete="current-password"
            />
            <p className="text-xs text-muted-foreground">
              Needed to move your email address - it is how you sign in and how a reset would
              reach you.
            </p>
          </Field>
        )}

        <div className="flex items-center gap-3">
          <Button type="submit" disabled={save.isPending}>
            {save.isPending && <Loader2Icon className="animate-spin" aria-hidden="true" />}
            Save changes
          </Button>
          {profile.isGoogleLinked && (
            <span className="flex items-center gap-1.5 text-xs text-muted-foreground">
              <ShieldCheckIcon className="size-3.5" aria-hidden="true" />
              Google is linked to this account
            </span>
          )}
        </div>
      </form>
    </DashboardPanel>
  )
}

function PasswordForm({ profile }: { profile: Profile }) {
  const queryClient = useQueryClient()
  const { applySession } = useAuth()
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmation, setConfirmation] = useState('')
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({})

  const mismatch = confirmation.length > 0 && confirmation !== newPassword

  const change = useMutation({
    mutationFn: () =>
      changePassword({
        currentPassword: profile.hasPassword ? currentPassword : undefined,
        newPassword,
      }),
    onSuccess: auth => {
      // The response carries a fresh token pair - every other session has just been ended.
      applySession(auth)
      queryClient.invalidateQueries({ queryKey: ['profile'] })
      setCurrentPassword('')
      setNewPassword('')
      setConfirmation('')
      setFieldErrors({})
      toast.success('Password changed. Your other devices have been signed out.')
    },
    onError: error => {
      if (error instanceof ApiError) {
        setFieldErrors(error.fieldErrors ?? {})
        toast.error(error.message)
      } else {
        toast.error('Could not change your password.')
      }
    },
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    if (!change.isPending && !mismatch) change.mutate()
  }

  return (
    <DashboardPanel>
      <form onSubmit={submit} className="flex flex-col gap-5">
        <div className="flex items-center gap-2">
          <KeyRoundIcon className="size-4 text-muted-foreground" aria-hidden="true" />
          <h2 className="font-heading text-base font-medium">
            {profile.hasPassword ? 'Change your password' : 'Set a password'}
          </h2>
        </div>

        <PanelDivider />

        {!profile.hasPassword && (
          <p className="flex items-start gap-2 rounded-xl border border-foreground/8 bg-muted/40 p-3 text-sm text-muted-foreground">
            <MailIcon className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
            You signed up with Google, so this account has no password yet. Setting one lets
            you sign in either way.
          </p>
        )}

        {profile.hasPassword && (
          <Field id="password-current" label="Current password" errors={fieldErrors.CurrentPassword ?? fieldErrors.PasswordMismatch}>
            <Input
              id="password-current"
              type="password"
              value={currentPassword}
              onChange={event => setCurrentPassword(event.target.value)}
              required
              autoComplete="current-password"
            />
          </Field>
        )}

        <Field id="password-new" label="New password" errors={fieldErrors.NewPassword ?? fieldErrors.PasswordTooShort}>
          <Input
            id="password-new"
            type="password"
            value={newPassword}
            onChange={event => setNewPassword(event.target.value)}
            minLength={8}
            required
            autoComplete="new-password"
          />
          <p className="text-xs text-muted-foreground">At least 8 characters.</p>
        </Field>

        <Field id="password-confirm" label="Repeat the new password" errors={mismatch ? ['These two do not match.'] : undefined}>
          <Input
            id="password-confirm"
            type="password"
            value={confirmation}
            onChange={event => setConfirmation(event.target.value)}
            required
            autoComplete="new-password"
            aria-invalid={mismatch}
          />
        </Field>

        <div>
          <Button type="submit" disabled={change.isPending || mismatch}>
            {change.isPending && <Loader2Icon className="animate-spin" aria-hidden="true" />}
            {profile.hasPassword ? 'Change password' : 'Set password'}
          </Button>
          <p className="pt-2 text-xs text-muted-foreground">
            This signs out every other device. You stay signed in here.
          </p>
        </div>
      </form>
    </DashboardPanel>
  )
}

function Field({
  id,
  label,
  errors,
  children,
}: {
  id: string
  label: string
  errors?: string[]
  children: React.ReactNode
}) {
  return (
    <div className="flex flex-col gap-2">
      <Label htmlFor={id}>{label}</Label>
      {children}
      {errors?.map(message => (
        <p key={message} role="alert" className="text-sm text-destructive">
          {message}
        </p>
      ))}
    </div>
  )
}

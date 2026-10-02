import { useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { Loader2Icon, MailWarningIcon, XIcon } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { resendConfirmation } from '@/features/auth/account-email-api'
import { ApiError } from '@/lib/api/client'
import { getProfile } from './account-api'

/**
 * A reminder for an address nobody has proved yet. It never blocks anything - the demo and
 * older accounts were never confirmed - but a forgotten password is only as recoverable as
 * the inbox behind it.
 */
export function ConfirmEmailBanner({ dismissible = true }: { dismissible?: boolean }) {
  const [hidden, setHidden] = useState(() => {
    try {
      return sessionStorage.getItem('foundu.confirmBannerHidden') === '1'
    } catch {
      return false
    }
  })
  const profile = useQuery({ queryKey: ['profile'], queryFn: getProfile, staleTime: 60_000, retry: false })
  const resend = useMutation({
    mutationFn: resendConfirmation,
    onSuccess: () => toast.success(`Sent to ${profile.data?.email}. Check your inbox - and spam, just in case.`),
    onError: (error) => toast.error(error instanceof ApiError ? error.message : 'Could not send it. Try again in a moment.'),
  })

  if (!profile.data || profile.data.emailConfirmed || (dismissible && hidden)) return null

  function hide() {
    setHidden(true)
    try {
      sessionStorage.setItem('foundu.confirmBannerHidden', '1')
    } catch {
      // Private mode - it just comes back next page load.
    }
  }

  return (
    <div
      role="status"
      className="mb-4 flex flex-wrap items-center gap-3 rounded-xl border border-amber-500/30 bg-amber-50 px-4 py-3 text-sm text-amber-950 dark:border-amber-400/25 dark:bg-amber-400/10 dark:text-amber-100"
    >
      <MailWarningIcon className="size-4 shrink-0" aria-hidden="true" />
      <p className="min-w-0 flex-1">
        Confirm <strong className="break-all">{profile.data.email}</strong> so you can reset your password if you ever forget it.
      </p>
      <Button size="sm" variant="outline" onClick={() => resend.mutate()} disabled={resend.isPending || resend.isSuccess}>
        {resend.isPending && <Loader2Icon className="animate-spin" aria-hidden="true" />}
        {resend.isSuccess ? 'Sent' : 'Send the link again'}
      </Button>
      {dismissible && (
        <button type="button" onClick={hide} aria-label="Hide for now" className="rounded-md p-1 opacity-70 hover:opacity-100">
          <XIcon className="size-4" aria-hidden="true" />
        </button>
      )}
    </div>
  )
}

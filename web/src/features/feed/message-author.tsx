import { Link } from 'react-router-dom'
import { LogInIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { useAuth } from '@/features/auth/use-auth'
import { MessageThread } from './message-thread'

/**
 * Message the author of a lost report - the sign-in gate the public feed leads to.
 *
 * Signed in, this is the finder's thread with the author: what they wrote, and any reply.
 * Nothing here exposes contact details in either direction. It is for "I have handed this in
 * at Security Desk A", not for arranging a handover, which is what the caution beside the
 * card says.
 */
export function MessageAuthor({ reportId, authorName }: { reportId: string; authorName: string }) {
  const { user } = useAuth()

  if (!user) {
    return (
      <div className="flex flex-col gap-3 rounded-xl border border-neutral-900/8 bg-white/70 p-4">
        <p className="text-sm text-neutral-600">
          Sign in to tell {authorName.split(' ')[0]} you have found it.
        </p>
        <Button
          className="bg-brand-forest text-white hover:bg-brand-forest/90"
          nativeButton={false}
          render={<Link to="/login" />}
        >
          <LogInIcon aria-hidden="true" />
          Sign in to message
        </Button>
      </div>
    )
  }

  return (
    <div className="flex flex-col gap-2">
      <p className="text-sm font-medium text-neutral-900">Tell {authorName.split(' ')[0]} where it went</p>
      <MessageThread reportId={reportId} isAuthor={false} />
    </div>
  )
}

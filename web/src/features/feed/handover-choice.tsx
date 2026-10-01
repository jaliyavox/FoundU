import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { HashIcon, Loader2Icon, MessageSquareIcon, ShieldCheckIcon } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { ApiError } from '@/lib/api/client'
import { displayCode } from './feed-api'
import { cancelHandover, getHandover, startHandover } from './handover-api'
import { MessageAuthor } from './message-author'

/**
 * What a finder does next, once they have said the item is theirs to hand over.
 *
 * Two ways forward, and they are not the same weight: writing to the owner costs nothing and
 * changes nothing, while taking it to a desk mints the code both sides will quote and pulls
 * the notice off the feed. So the code is not shown until they commit to the walk.
 */
export function HandoverChoice({
  reportId,
  authorName,
  signedIn,
}: {
  reportId: string
  authorName: string
  signedIn: boolean
}) {
  const queryClient = useQueryClient()

  const { data: handover, isPending } = useQuery({
    queryKey: ['handover', reportId],
    queryFn: () => getHandover(reportId),
    enabled: signedIn,
  })

  const start = useMutation({
    mutationFn: () => startHandover(reportId),
    onSuccess: result => {
      queryClient.setQueryData(['handover', reportId], result)
      queryClient.invalidateQueries({ queryKey: ['lost-feed'] })
      toast.success('Code ready. Take the item to any campus desk.')
    },
    onError: error =>
      toast.error(error instanceof ApiError ? error.message : 'Could not start the handover.'),
  })

  const cancel = useMutation({
    mutationFn: () => cancelHandover(reportId),
    onSuccess: result => {
      queryClient.setQueryData(['handover', reportId], result)
      queryClient.invalidateQueries({ queryKey: ['lost-feed'] })
      toast.success('Called off. The notice is back on the feed.')
    },
    onError: error =>
      toast.error(error instanceof ApiError ? error.message : 'Could not cancel.'),
  })

  // Not signed in: the confirmation has been read, and this is where it stops.
  if (!signedIn) {
    return (
      <div className="fu-appear flex flex-col gap-4">
        <p className="text-sm text-pretty text-neutral-700">
          Thank you. Sign in to write to {authorName.split(' ')[0]} or to get the code for
          handing it to security - both need an account, so the desk knows who brought it in.
        </p>
        <div className="flex flex-wrap gap-2">
          <Button className="bg-brand-forest text-white hover:bg-brand-forest/90" nativeButton={false} render={<Link to="/login" />}>
            Sign in to continue
          </Button>
          <Button variant="outline" className="border-neutral-900/15 bg-white/70 text-neutral-800 hover:bg-white" nativeButton={false} render={<Link to="/register" />}>
            Create an account
          </Button>
        </div>
      </div>
    )
  }

  const live = handover && (handover.status === 'AwaitingHandIn' || handover.status === 'InCustody')

  return (
    <div className="fu-appear flex flex-col gap-5">
      {live && handover.code ? (
        <>
          <div className="flex items-center justify-between gap-4 rounded-xl border border-brand-forest/15 bg-brand-forest px-4 py-3 text-white">
            <div>
              <p className="text-xs text-white/70">
                {handover.status === 'InCustody' ? 'At the desk under this code' : 'Quote this at the desk'}
              </p>
              <p className="font-mono text-2xl font-semibold tracking-[0.2em] tabular-nums">
                {displayCode(handover.code)}
              </p>
            </div>
            <HashIcon className="size-6 shrink-0 text-white/60" aria-hidden="true" />
          </div>

          <p className="text-sm text-pretty text-neutral-600">
            {handover.status === 'InCustody' ? (
              <>
                The desk has it{handover.storageLocationName ? ` at ${handover.storageLocationName}` : ''}.
                {' '}{authorName.split(' ')[0]} has the same code and can collect it with their
                student ID. Nothing more for you to do - thank you.
              </>
            ) : (
              <>
                {authorName.split(' ')[0]} has this code too. Take the item to any campus desk
                and quote it; they will collect it there with their student ID. The notice is
                off the feed while you do.
              </>
            )}
          </p>

          {handover.status === 'AwaitingHandIn' && (
            <Button
              variant="ghost"
              size="sm"
              className="self-start text-neutral-600 hover:text-neutral-900"
              disabled={cancel.isPending}
              onClick={() => cancel.mutate()}
            >
              {cancel.isPending && <Loader2Icon className="animate-spin" aria-hidden="true" />}
              I cannot take it after all
            </Button>
          )}
        </>
      ) : (
        <>
          <p className="text-sm text-pretty text-neutral-700">
            Two ways to get it back to {authorName.split(' ')[0]}. Either is fine - the desk
            route is the one that does not need you two to meet.
          </p>

          <Button
            size="lg"
            className="justify-start bg-brand-forest text-white hover:bg-brand-forest/90"
            disabled={start.isPending || isPending}
            onClick={() => start.mutate()}
          >
            {start.isPending ? (
              <Loader2Icon className="animate-spin" aria-hidden="true" />
            ) : (
              <ShieldCheckIcon aria-hidden="true" />
            )}
            Hand it to security
          </Button>
          <p className="-mt-3 text-xs text-neutral-500">
            Generates a code for you and {authorName.split(' ')[0]}. The notice pauses while
            you walk it over, and comes back if you do not.
          </p>
        </>
      )}

      <div className="border-t border-neutral-900/8 pt-5">
        <p className="flex items-center gap-2 pb-3 text-sm font-medium">
          <MessageSquareIcon className="size-4 text-neutral-500" aria-hidden="true" />
          Or write to {authorName.split(' ')[0]}
        </p>
        <MessageAuthor reportId={reportId} authorName={authorName} />
      </div>
    </div>
  )
}

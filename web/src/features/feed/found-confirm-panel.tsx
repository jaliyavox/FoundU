import { useMutation } from '@tanstack/react-query'
import { Loader2Icon, TriangleAlertIcon } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { ApiError } from '@/lib/api/client'
import { cn } from '@/lib/utils'
import { formatWindow, registerFoundClaim, type LostReportFeedItem } from './feed-api'

/**
 * The check before "I found this" is recorded.
 *
 * It sits beside the spotlit card, on the same dashed chain as the hand-in steps, rather than
 * as a centred dialog - the finder is being asked to compare what is in their hand against
 * that card, so taking the card off screen to ask the question was exactly backwards.
 *
 * Pressing it tells a real person their lost thing has turned up, so a mis-tap or a
 * near-enough match costs them a false hope. This restates the identifying details and asks
 * the finder to match them before it goes any further.
 */
export function FoundConfirmPanel({
  item,
  onCancel,
  onConfirmed,
  className,
}: {
  item: LostReportFeedItem
  onCancel: () => void
  onConfirmed: () => void
  className?: string
}) {
  const firstName = item.postedByName.split(' ')[0]

  // Recorded before the steps appear: the author's card should update the moment a finder
  // commits, not only if they go on to write a message.
  const claim = useMutation({
    mutationFn: () => registerFoundClaim(item.id),
    onSuccess: onConfirmed,
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : 'Could not reach the server.'),
  })

  return (
    <div
      data-feed-middle="true"
      className={cn(
        'fu-spotlight-in pointer-events-auto flex w-full flex-col gap-4 rounded-2xl bg-linear-to-b from-white to-brand-mist p-5 text-neutral-900 shadow-2xl shadow-black/40 ring-1 ring-neutral-900/8',
        className,
      )}
    >
      <div>
        <h2 className="font-heading text-base font-medium">Are you sure you found this?</h2>
        <p className="pt-1 text-sm text-pretty text-neutral-600">
          {firstName} is told straight away that their {item.itemTypeName.toLowerCase()} has
          turned up. Check it against the card first.
        </p>
      </div>

      <dl className="flex flex-col gap-2 rounded-xl border border-neutral-900/8 bg-white/70 p-3 text-sm">
        <div className="flex gap-3">
          <dt className="w-24 shrink-0 text-neutral-500">Item</dt>
          <dd>
            {item.itemTypeName}
            {item.primaryColor && `, ${item.primaryColor.toLowerCase()}`}
          </dd>
        </div>
        <div className="flex gap-3">
          <dt className="w-24 shrink-0 text-neutral-500">Last seen</dt>
          <dd>{item.lastSeenLocationName}</dd>
        </div>
        <div className="flex gap-3">
          <dt className="w-24 shrink-0 text-neutral-500">Lost between</dt>
          <dd>{formatWindow(item.estimatedLostFromAt, item.estimatedLostToAt)}</dd>
        </div>
      </dl>

      <p className="flex items-start gap-2 text-xs text-neutral-500">
        <TriangleAlertIcon className="mt-0.5 size-3.5 shrink-0" aria-hidden="true" />
        If it only looks similar, stop here. A near-enough match sends the wrong person across
        campus.
      </p>

      <div className="flex flex-wrap gap-2">
        <Button
          size="sm"
          className="bg-brand-forest text-white hover:bg-brand-forest/90"
          onClick={() => claim.mutate()}
          disabled={claim.isPending}
        >
          {claim.isPending && <Loader2Icon className="animate-spin" aria-hidden="true" />}
          Yes, continue
        </Button>
        <Button
          size="sm"
          variant="ghost"
          className="text-neutral-600 hover:bg-neutral-900/5 hover:text-neutral-900"
          onClick={onCancel}
          disabled={claim.isPending}
        >
          Not sure yet
        </Button>
      </div>
    </div>
  )
}

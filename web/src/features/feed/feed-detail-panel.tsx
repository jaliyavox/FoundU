import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useMutation } from '@tanstack/react-query'
import { toast } from 'sonner'
import { ActivityIcon, ArrowLeftIcon, ClockIcon, HandHeartIcon, MapPinIcon } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from '@/components/ui/sheet'
import { useAuth } from '@/features/auth/use-auth'
import { useIsMobile } from '@/hooks/use-mobile'
import { cn } from '@/lib/utils'
import { formatWindow, registerFoundClaim, timeAgo, type LostReportFeedItem } from './feed-api'
import { FoundConfirmDialog } from './found-confirm-dialog'
import { ItemMedia } from './item-media'
import { ZoomButton } from './photo-lightbox'
import { ApiError } from '@/lib/api/client'
import { MessageAuthor } from './message-author'

/**
 * Post detail as a right-hand side panel. The feed stays on screen behind it, so the
 * reading position survives - which a centred modal loses.
 *
 * The "I found this" flow deliberately routes the finder to a desk rather than to the
 * poster: verification depends on staff holding a detail the claimant must describe from
 * memory, and a direct handover skips that and puts two strangers in contact.
 */

export function FeedDetailPanel({
  item,
  showHandIn,
  onShowHandIn,
  onZoom,
  onClose,
}: {
  item: LostReportFeedItem | null
  /** Lifted to the page: the spotlight card shows the matching caution beside itself. */
  showHandIn: boolean
  onShowHandIn: (show: boolean) => void
  /** Also lifted: the full photo opens next to the spotlight card, not over this panel. */
  onZoom: () => void
  onClose: () => void
}) {
  const { user } = useAuth()
  const isMobile = useIsMobile()
  const [confirming, setConfirming] = useState(false)

  // Recorded before the steps appear: the author's card should update the moment a finder
  // commits, not only if they go on to write a message.
  const claim = useMutation({
    mutationFn: (reportId: string) => registerFoundClaim(reportId),
    onSuccess: () => {
      setConfirming(false)
      onShowHandIn(true)
    },
    onError: (error) => {
      toast.error(error instanceof ApiError ? error.message : 'Could not reach the server.')
    },
  })

  return (
    <Sheet open={item !== null} onOpenChange={(open) => !open && onClose()}>
      <SheetContent
        side={isMobile ? 'bottom' : 'right'}
        className={cn(
          'w-full gap-0 overflow-y-auto border-brand-forest/10 bg-linear-to-b from-white to-brand-mist text-neutral-900',
          // Bottom sheet stops short of the top so the spotlight card stays visible above
          // it, and gives up more room once the steps appear.
          //
          // The height must be written as a data-[side=bottom] variant: the base component
          // sets `data-[side=bottom]:h-auto`, and a plain `h-*` loses to it on specificity -
          // which left the sheet content-sized and overlapping the steps.
          isMobile && 'rounded-t-3xl transition-[height] duration-500 ease-out',
          isMobile &&
            (showHandIn ? 'data-[side=bottom]:h-[46svh]' : 'data-[side=bottom]:h-[76svh]'),
          !isMobile && 'sm:max-w-md',
        )}
      >
        {item && (
          <>
            <div
              className={cn(
                'relative aspect-video shrink-0 overflow-hidden border-b border-brand-forest/10 bg-linear-to-br from-brand-mist via-white to-brand-sage/45',
                isMobile && 'hidden',
              )}
            >
              <div
                aria-hidden="true"
                className="pointer-events-none absolute -top-10 -right-8 size-48 rounded-full bg-brand-green/25 blur-2xl"
              />
              <ItemMedia
                photoUrl={item.photoUrls?.[0]}
                itemType={item.itemTypeName}
                category={item.categoryName}
                illustrationClassName="absolute inset-0 m-auto size-28 text-brand-forest/65"
              />

              {/* Only over a real photo - there is nothing to zoom into on an illustration. */}
              {item.photoUrls?.length > 0 && <ZoomButton onClick={onZoom} />}
            </div>

            <div className="flex flex-col gap-5 p-6">
              <SheetHeader className="gap-1 p-0">
                <SheetTitle className="text-xl text-neutral-900">{item.itemTypeName}</SheetTitle>
                <SheetDescription className="text-neutral-500">
                  Reported by {item.postedByName} · {timeAgo(item.createdAt)}
                </SheetDescription>
              </SheetHeader>

              {/* The hero image is hidden on mobile to keep the sheet short, so the photo
                  gets a compact strip here instead - otherwise there is no way to see it
                  on a phone at all. */}
              {isMobile && item.photoUrls?.length > 0 && (
                <div className="relative h-28 overflow-hidden rounded-xl border border-neutral-900/8">
                  <ItemMedia
                    photoUrl={item.photoUrls[0]}
                    itemType={item.itemTypeName}
                    category={item.categoryName}
                  />
                  <ZoomButton onClick={onZoom} className="right-2 bottom-2 size-8" />
                </div>
              )}

              <div className="flex flex-wrap gap-1.5">
                <Badge variant="secondary" className="bg-neutral-900/6 text-neutral-700">
                  {item.categoryName}
                </Badge>
                {item.primaryColor && (
                  <Badge variant="secondary" className="bg-neutral-900/6 text-neutral-700">
                    {item.primaryColor}
                  </Badge>
                )}
              </div>

              <p className="text-sm leading-relaxed text-pretty text-neutral-700">{item.description}</p>

              <dl className="flex flex-col gap-3 rounded-xl border border-neutral-900/8 bg-white/70 p-4">
                <div className="flex items-start gap-3">
                  <MapPinIcon className="mt-0.5 size-4 shrink-0 text-brand-green" aria-hidden="true" />
                  <div>
                    <dt className="text-xs text-neutral-500">Last seen</dt>
                    <dd className="text-sm text-neutral-900">{item.lastSeenLocationName}</dd>
                  </div>
                </div>
                <div className="flex items-start gap-3">
                  <ClockIcon className="mt-0.5 size-4 shrink-0 text-brand-green" aria-hidden="true" />
                  <div>
                    <dt className="text-xs text-neutral-500">Lost between</dt>
                    <dd className="text-sm text-neutral-900">
                      {formatWindow(item.estimatedLostFromAt, item.estimatedLostToAt)}
                    </dd>
                  </div>
                </div>
              </dl>

              {item.isMine ? (
                /* Your own post. "I found this" would be nonsense here, and the API refuses
                   both the hand-in message and a self-message anyway - so the slot holds the
                   one action that does make sense: go and watch its progress. */
                <>
                  <Button
                    size="lg"
                    className="bg-brand-forest text-white hover:bg-brand-forest/90"
                    nativeButton={false}
                    render={<Link to="/my-reports" />}
                  >
                    <ActivityIcon aria-hidden="true" />
                    View status
                  </Button>

                  <p className="text-sm text-pretty text-neutral-500">
                    This is your report. Its progress - and anyone who says they have found it -
                    shows on your reports page.
                  </p>
                </>
              ) : !showHandIn ? (
                <Button
                  size="lg"
                  className="bg-brand-forest text-white hover:bg-brand-forest/90"
                  onClick={() => (user ? setConfirming(true) : onShowHandIn(true))}
                >
                  <HandHeartIcon aria-hidden="true" />
                  I found this
                </Button>
              ) : (
                <div className="fu-reveal flex flex-col gap-5">
                  <p className="text-sm text-pretty text-neutral-600">
                    The three steps are shown with the card. Hand it in first - then tell
                    {' '}{item.postedByName.split(' ')[0]} where it went.
                  </p>

                  {/* Messaging only opens once someone says they found it. Before that the
                      panel is a notice board, and a message box invites contact for its own
                      sake rather than to report a hand-in. */}
                  <div className="border-t border-neutral-900/8 pt-5">
                    <MessageAuthor reportId={item.id} authorName={item.postedByName} />
                  </div>

                  <div className="flex flex-wrap gap-2">
                    <Button
                      variant="outline"
                      className="border-neutral-900/15 bg-white/70 text-neutral-800 hover:bg-white"
                      onClick={() => onShowHandIn(false)}
                    >
                      <ArrowLeftIcon aria-hidden="true" />
                      Back
                    </Button>
                    {!user && (
                      <Button
                        className="bg-brand-forest text-white hover:bg-brand-forest/90"
                        nativeButton={false}
                        render={<Link to="/register" />}
                      >
                        Create an account
                      </Button>
                    )}
                  </div>
                </div>
              )}
            </div>
          </>
        )}
      </SheetContent>

      {item && user && (
        <FoundConfirmDialog
          item={item}
          open={confirming}
          onOpenChange={(open) => !open && setConfirming(false)}
          onConfirm={() => claim.mutate(item.id)}
          isPending={claim.isPending}
        />
      )}
    </Sheet>
  )
}

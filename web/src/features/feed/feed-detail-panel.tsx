import { Link } from 'react-router-dom'
import { ArrowLeftIcon, ClockIcon, HandHeartIcon, MapPinIcon } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from '@/components/ui/sheet'
import { useAuth } from '@/features/auth/use-auth'
import { useIsMobile } from '@/hooks/use-mobile'
import { cn } from '@/lib/utils'
import { formatWindow, timeAgo, type LostReportFeedItem } from './feed-api'
import { ItemIllustration } from './item-illustration'
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
  onClose,
}: {
  item: LostReportFeedItem | null
  /** Lifted to the page: the spotlight card shows the matching caution beside itself. */
  showHandIn: boolean
  onShowHandIn: (show: boolean) => void
  onClose: () => void
}) {
  const { user } = useAuth()
  const isMobile = useIsMobile()

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
              <ItemIllustration
                itemType={item.itemTypeName}
                category={item.categoryName}
                className="absolute inset-0 m-auto size-28 text-brand-forest/65"
              />
            </div>

            <div className="flex flex-col gap-5 p-6">
              <SheetHeader className="gap-1 p-0">
                <SheetTitle className="text-xl text-neutral-900">{item.itemTypeName}</SheetTitle>
                <SheetDescription className="text-neutral-500">
                  Reported by {item.postedByName} · {timeAgo(item.createdAt)}
                </SheetDescription>
              </SheetHeader>

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

              {!showHandIn ? (
                <>
                  <Button
                    size="lg"
                    className="bg-brand-forest text-white hover:bg-brand-forest/90"
                    onClick={() => onShowHandIn(true)}
                  >
                    <HandHeartIcon aria-hidden="true" />
                    I found this
                  </Button>

                  <div className="border-t border-neutral-900/8 pt-5">
                    <MessageAuthor reportId={item.id} authorName={item.postedByName} />
                  </div>
                </>
              ) : (
                <div className="flex flex-col gap-5">
                  <p className="text-sm text-pretty text-neutral-600">
                    The three steps are shown with the card. Nothing else to do here - take it
                    to a desk and staff will handle the rest.
                  </p>

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
    </Sheet>
  )
}

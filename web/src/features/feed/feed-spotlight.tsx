import { createPortal } from 'react-dom'
import { useIsMobile } from '@/hooks/use-mobile'
import { timeAgo, type LostReportFeedItem } from './feed-api'
import { HandInSteps } from './hand-in-steps'
import { ItemIllustration } from './item-illustration'

/**
 * The chosen post, lifted out of the grid while the side panel is open.
 *
 * Desktop: one fixed spot, centred in the space left of the panel, with the hand-in steps
 * beside it. Leaving the card in the grid meant a right-column card ended up under the panel.
 *
 * Mobile: there is no room beside anything, so the composition rotates - a compact card
 * pinned above the bottom sheet, with the steps inside the sheet instead.
 *
 * Purely presentational: the panel carries the real content and controls, so this is hidden
 * from assistive technology and takes no pointer events.
 */

/** Matches the panel's `sm:max-w-md`. */
const PANEL_WIDTH = '28rem'

export function FeedSpotlight({
  item,
  showHandIn,
}: {
  item: LostReportFeedItem | null
  showHandIn: boolean
}) {
  const isMobile = useIsMobile()

  if (!item) return null

  const meta = [item.primaryColor, item.lastSeenLocationName].filter(Boolean).join(' · ')

  const initials = item.postedByName
    .trim()
    .split(/\s+/)
    .map((part) => part[0])
    .slice(0, 2)
    .join('')
    .toUpperCase()

  /* ------------------------------------------------------------------ mobile */
  if (isMobile) {
    return createPortal(
      <div
        aria-hidden="true"
        className="pointer-events-none fixed inset-x-0 top-0 z-60 flex flex-col items-center gap-4 px-4 pt-5"
      >
        <div
          data-feed-spotlight={item.id}
          key={item.id}
          className="fu-spotlight-in flex w-full max-w-sm items-center gap-3 rounded-2xl bg-[oklch(0.21_0.03_148)] p-3 shadow-2xl shadow-black/50 ring-2 ring-brand-green/60"
        >
          <span className="relative flex size-14 shrink-0 items-center justify-center overflow-hidden rounded-xl bg-[oklch(0.25_0.035_148)]">
            <ItemIllustration
              itemType={item.itemTypeName}
              category={item.categoryName}
              className="size-8 text-brand-sage/75"
            />
          </span>

          <div className="min-w-0 flex-1">
            <p className="truncate font-medium text-white">{item.itemTypeName}</p>
            <p className="truncate text-sm text-white/50">{meta}</p>
          </div>
        </div>

        {/* Same block as desktop, stacked under the card instead of beside it. The sheet
            shrinks to make room - see feed-detail-panel.tsx. */}
        {showHandIn && (
          <div
            data-feed-steps="true"
            className="fu-spotlight-in w-full max-w-sm rounded-2xl bg-linear-to-b from-white to-brand-mist p-4 shadow-2xl shadow-black/40 ring-1 ring-neutral-900/8"
          >
            <HandInSteps tone="light" />
          </div>
        )}
      </div>,
      document.body,
    )
  }

  /* ----------------------------------------------------------------- desktop */
  return createPortal(
    <div
      aria-hidden="true"
      className="pointer-events-none fixed inset-y-0 left-0 z-60 flex items-center justify-center px-6"
      style={{ width: `calc(100vw - ${PANEL_WIDTH})` }}
    >
      <div className="flex flex-col items-center gap-8 lg:flex-row lg:items-center">
        <div
          data-feed-spotlight={item.id}
          // Keyed on the id so switching cards replays the entrance.
          key={item.id}
          className="fu-spotlight-in w-full max-w-xs overflow-hidden rounded-2xl bg-[oklch(0.21_0.03_148)] shadow-2xl shadow-black/50 ring-2 ring-brand-green/60"
        >
          <div className="relative aspect-4/3 overflow-hidden bg-[oklch(0.25_0.035_148)]">
            <ItemIllustration
              itemType={item.itemTypeName}
              category={item.categoryName}
              className="absolute inset-0 m-auto size-20 text-brand-sage/70"
            />
          </div>

          <div className="flex flex-col gap-2 p-5">
            <h3 className="text-lg leading-snug font-medium text-white">{item.itemTypeName}</h3>
            <p className="text-sm text-white/55">{meta}</p>
            <p className="line-clamp-2 text-sm leading-relaxed text-pretty text-white/70">
              {item.description}
            </p>

            <div className="flex items-center gap-2 pt-3 text-xs text-white/40">
              <span className="flex size-5 items-center justify-center rounded-full bg-brand-green/20 text-[10px] font-medium text-brand-green">
                {initials}
              </span>
              <span className="truncate">{item.postedByName}</span>
              <span>·</span>
              <span className="shrink-0">{timeAgo(item.createdAt)}</span>
            </div>
          </div>
        </div>

        {/* Steps beside the card, joined by the connector, so the flow reads left to right. */}
        {showHandIn && (
          <div
            data-feed-steps="true"
            className="fu-spotlight-in w-full max-w-[17rem] rounded-2xl bg-linear-to-b from-white to-brand-mist p-5 shadow-2xl shadow-black/40 ring-1 ring-neutral-900/8"
          >
            <HandInSteps tone="light" />
          </div>
        )}
      </div>
    </div>,
    document.body,
  )
}

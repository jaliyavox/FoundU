import { createPortal } from 'react-dom'
import {
  ClipboardCheckIcon,
  HandHeartIcon,
  ShieldCheckIcon,
  TriangleAlertIcon,
} from 'lucide-react'
import { timeAgo, type LostReportFeedItem } from './feed-api'
import { ItemIllustration } from './item-illustration'

/**
 * The chosen post, lifted out of the grid to one fixed spot while the side panel is open.
 *
 * Leaving the card where it sat meant a right-column card ended up underneath the panel.
 * Every card now animates to the same place - centred in the space left of the panel - so
 * the connector geometry is predictable no matter which card was clicked.
 *
 * Purely presentational: the panel carries the real content and controls, so this is hidden
 * from assistive technology and takes no pointer events.
 */

/** Matches the panel's `sm:max-w-md`. */
const PANEL_WIDTH = '28rem'

const HANDIN_STEPS = [
  { icon: HandHeartIcon, title: 'Hand it in', body: 'Any campus desk will do.' },
  { icon: ClipboardCheckIcon, title: 'Staff log it', body: 'With a detail only the owner knows.' },
  { icon: ShieldCheckIcon, title: 'Owner collects', body: 'They name that detail to claim it.' },
]

export function FeedSpotlight({
  item,
  showHandIn,
}: {
  item: LostReportFeedItem | null
  showHandIn: boolean
}) {
  if (!item) return null

  const meta = [item.primaryColor, item.lastSeenLocationName].filter(Boolean).join(' · ')

  const initials = item.postedByName
    .trim()
    .split(/\s+/)
    .map((part) => part[0])
    .slice(0, 2)
    .join('')
    .toUpperCase()

  return createPortal(
    <div
      aria-hidden="true"
      className="pointer-events-none fixed inset-y-0 left-0 z-60 hidden items-center justify-center px-6 sm:flex"
      style={{ width: `calc(100vw - ${PANEL_WIDTH})` }}
    >
      <div className="flex flex-col items-center gap-8 lg:flex-row lg:items-center">
      <div className="w-full max-w-xs">
      <div
        data-feed-spotlight={item.id}
        // Keyed on the id so switching cards replays the entrance.
        key={item.id}
        className="fu-spotlight-in overflow-hidden rounded-2xl bg-[oklch(0.21_0.03_148)] shadow-2xl shadow-black/50 ring-2 ring-brand-green/60"
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

      </div>

      {/* Steps sit beside the card, joined to it by the animated connector, so the flow
          reads left to right: the item, what to do with it, then the panel. */}
      {showHandIn && (
        <div
          data-feed-steps="true"
          className="fu-spotlight-in w-full max-w-[17rem] rounded-2xl bg-[oklch(0.21_0.03_148)] p-5 shadow-2xl shadow-black/50 ring-1 ring-white/10"
        >
          <ol className="flex flex-col gap-3">
            {HANDIN_STEPS.map(({ icon: Icon, title, body }, index) => (
              <li
                key={title}
                className="fu-swap-in relative flex items-center gap-3"
                style={{ animationDelay: `${index * 70}ms` }}
              >
                {index < HANDIN_STEPS.length - 1 && (
                  <span
                    className="fu-rail-in absolute top-1/2 left-4 h-[calc(100%+0.75rem)] w-px bg-linear-to-b from-brand-green/45 to-brand-green/15"
                    style={{ animationDelay: `${index * 70 + 120}ms` }}
                  />
                )}

                <span className="relative z-10 flex size-8 shrink-0 items-center justify-center rounded-lg bg-brand-green/15 text-brand-green ring-4 ring-[oklch(0.21_0.03_148)]">
                  <Icon className="size-4" />
                </span>
                <div className="min-w-0">
                  <p className="text-sm font-medium text-white/90">{title}</p>
                  <p className="text-sm text-white/50">{body}</p>
                </div>
              </li>
            ))}
          </ol>

          <p
            className="fu-swap-in mt-4 flex items-start gap-2 border-t border-white/10 pt-4 text-sm text-pretty text-amber-100/80"
            style={{ animationDelay: '210ms' }}
          >
            <TriangleAlertIcon className="mt-0.5 size-4 shrink-0 text-amber-300" />
            Hand it to a desk, not the owner directly.
          </p>
        </div>
      )}
      </div>
    </div>,
    document.body,
  )
}
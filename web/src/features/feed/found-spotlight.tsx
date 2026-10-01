import { createPortal } from 'react-dom'
import { useIsMobile } from '@/hooks/use-mobile'
import { ItemIllustration } from './item-illustration'
import { timeAgo, type FoundPostItem } from './feed-api'

/**
 * The found post lifted out of the board while its panel is open - the same composition the
 * lost feed uses, so opening either board feels like the same gesture.
 *
 * Simpler than the lost spotlight because a found post has no hand-in steps and no photo of
 * its own: it is card, chain, panel. Presentational only - the panel holds the conversation
 * and every control - so it is hidden from assistive technology and takes no pointer events.
 */

/** Matches the panel's `sm:max-w-md`. */
const PANEL_WIDTH = '28rem'

export function FoundSpotlight({ item }: { item: FoundPostItem | null }) {
  const isMobile = useIsMobile()

  if (!item) return null

  const meta = [item.primaryColor, item.foundLocationName].filter(Boolean).join(' · ')

  /* ------------------------------------------------------------------ mobile */
  if (isMobile) {
    // No room beside anything, so the card pins above the sheet and the chain drops.
    return createPortal(
      <div className="pointer-events-none fixed inset-x-0 top-0 z-60 flex flex-col items-center px-4 pt-5">
        <div
          aria-hidden="true"
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
      </div>,
      document.body,
    )
  }

  /* ----------------------------------------------------------------- desktop */
  return createPortal(
    <div
      className="pointer-events-none fixed inset-y-0 left-0 z-60 flex items-center justify-center px-6"
      style={{ width: `calc(100vw - ${PANEL_WIDTH})` }}
    >
      <div
        aria-hidden="true"
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
          <span className="absolute top-3 left-3 rounded-full bg-amber-400/90 px-2.5 py-1 text-[11px] font-semibold text-neutral-900">
            {item.isMine ? 'Your post' : 'Not at a desk yet'}
          </span>
        </div>

        <div className="flex flex-col gap-2 p-5">
          <h3 className="text-lg leading-snug font-medium text-white">{item.itemTypeName}</h3>
          <p className="text-sm text-white/55">{meta}</p>
          <p className="line-clamp-2 text-sm leading-relaxed text-pretty text-white/70">
            {item.description}
          </p>

          <div className="flex items-center gap-2 pt-3 text-xs text-white/40">
            <span className="truncate">Found by {item.isMine ? 'you' : item.postedByName}</span>
            <span>·</span>
            <span className="shrink-0">{timeAgo(item.createdAt)}</span>
          </div>
        </div>
      </div>
    </div>,
    document.body,
  )
}

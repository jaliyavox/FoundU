import { ArrowUpRightIcon } from 'lucide-react'
import { timeAgo, type LostReportFeedItem } from './feed-api'
import { ItemIllustration } from './item-illustration'
import { cn } from '@/lib/utils'

/** Collapsed feed post. Details open in a side panel - see feed-detail-panel.tsx. */
export function FeedCard({
  item,
  isActive,
  onOpen,
}: {
  item: LostReportFeedItem
  isActive: boolean
  onOpen: () => void
}) {
  // Colour and place are what actually help someone recognise an item.
  const meta = [item.primaryColor, item.lastSeenLocationName].filter(Boolean).join(' · ')

  const initials = item.postedByName
    .trim()
    .split(/\s+/)
    .map((part) => part[0])
    .slice(0, 2)
    .join('')
    .toUpperCase()

  return (
    // A button, not a div with onClick - it has to be reachable and operable by keyboard.
    <button
      type="button"
      onClick={onOpen}
      data-feed-card={item.id}
      aria-label={`${item.itemTypeName} lost near ${item.lastSeenLocationName}. Open details.`}
      className={cn(
        'group relative flex h-full w-full flex-col overflow-hidden rounded-2xl bg-[oklch(0.21_0.03_148)] text-left',
        'transition duration-300 focus-visible:ring-2 focus-visible:ring-brand-green focus-visible:outline-none',
        isActive
          ? 'ring-2 ring-brand-green/60 shadow-xl shadow-brand-forest/25'
          : 'ring-1 ring-white/8 hover:-translate-y-1 hover:ring-white/20 hover:shadow-xl hover:shadow-brand-forest/25',
      )}
    >
      {/* Glow blooming from the top edge, so clickability reads before the cursor lands. */}
      <span
        aria-hidden="true"
        className="pointer-events-none absolute -inset-px rounded-2xl opacity-0 transition-opacity duration-300 group-hover:opacity-100"
        style={{
          background:
            'radial-gradient(ellipse 80% 60% at 50% 0%, rgba(100,188,109,0.22), transparent 70%)',
        }}
      />

      <div className="relative aspect-4/3 overflow-hidden bg-[oklch(0.25_0.035_148)]">
        <ItemIllustration
          itemType={item.itemTypeName}
          category={item.categoryName}
          className="absolute inset-0 m-auto size-20 text-brand-sage/70 transition-transform duration-300 group-hover:scale-105"
        />
      </div>

      <div className="relative flex flex-1 flex-col gap-2 p-5">
        {/* Primary: what was lost. */}
        <div className="flex items-start justify-between gap-3">
          <h3 className="text-lg leading-snug font-medium text-white">{item.itemTypeName}</h3>
          <ArrowUpRightIcon
            className="mt-1 size-4 shrink-0 text-white/30 transition-all duration-300 group-hover:translate-x-0.5 group-hover:-translate-y-0.5 group-hover:text-brand-green"
            aria-hidden="true"
          />
        </div>

        {/* Secondary: the details that make it recognisable. */}
        <p className="text-sm text-white/55">{meta}</p>

        {/* Body. Two lines keeps every card the same height. */}
        <p className="line-clamp-2 flex-1 text-sm leading-relaxed text-pretty text-white/70">
          {item.description}
        </p>

        {/* Tertiary: who and when. */}
        <div className="flex items-center gap-2 pt-3 text-xs text-white/40">
          <span className="flex size-5 items-center justify-center rounded-full bg-brand-green/20 text-[10px] font-medium text-brand-green">
            {initials}
          </span>
          <span className="truncate">{item.postedByName}</span>
          <span aria-hidden="true">·</span>
          <span className="shrink-0">{timeAgo(item.createdAt)}</span>
        </div>
      </div>
    </button>
  )
}

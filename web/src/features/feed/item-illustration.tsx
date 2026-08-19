import type { ComponentType } from 'react'
import { cn } from '@/lib/utils'

/**
 * Monoline placeholder artwork for a feed post, chosen from the item type.
 *
 * There is no photo upload yet - the domain has a LostItemPhoto entity but no endpoint - so
 * every card gets an illustration instead of an empty box. Swap this for the real image once
 * uploads land, keeping it as the fallback when a post has no photo.
 */

const stroke = {
  fill: 'none',
  stroke: 'currentColor',
  strokeWidth: 2,
  strokeLinecap: 'round',
  strokeLinejoin: 'round',
} as const

type Art = ComponentType<{ className?: string }>

const Backpack: Art = ({ className }) => (
  <svg viewBox="0 0 64 64" className={className}>
    <g {...stroke}>
      <rect x="16" y="22" width="32" height="32" rx="9" />
      <path d="M16 32 q16 -12 32 0" />
      <path d="M25 22 v-4 a7 7 0 0 1 14 0 v4" />
      <rect x="24" y="38" width="16" height="10" rx="4" />
      <path d="M32 38 v10" opacity={0.45} />
    </g>
  </svg>
)

const Wallet: Art = ({ className }) => (
  <svg viewBox="0 0 64 64" className={className}>
    <g {...stroke}>
      <rect x="12" y="20" width="40" height="26" rx="6" />
      <path d="M12 28 h40" opacity={0.45} />
      <circle cx="42" cy="37" r="3.5" />
      <path d="M18 20 v-3 a3 3 0 0 1 3 -3 h20" opacity={0.55} />
    </g>
  </svg>
)

const Phone: Art = ({ className }) => (
  <svg viewBox="0 0 64 64" className={className}>
    <g {...stroke}>
      <rect x="21" y="10" width="22" height="44" rx="6" />
      <path d="M28 15 h8" />
      <path d="M28 48 h8" opacity={0.45} />
    </g>
  </svg>
)

const Laptop: Art = ({ className }) => (
  <svg viewBox="0 0 64 64" className={className}>
    <g {...stroke}>
      <rect x="15" y="17" width="34" height="23" rx="4" />
      <path d="M9 46 h46 a3 3 0 0 1 -3 4 h-40 a3 3 0 0 1 -3 -4 z" />
      <path d="M15 40 h34" opacity={0.45} />
    </g>
  </svg>
)

const Headphones: Art = ({ className }) => (
  <svg viewBox="0 0 64 64" className={className}>
    <g {...stroke}>
      <path d="M15 40 v-6 a17 17 0 0 1 34 0 v6" />
      <rect x="10" y="38" width="10" height="16" rx="5" />
      <rect x="44" y="38" width="10" height="16" rx="5" />
    </g>
  </svg>
)

const Keys: Art = ({ className }) => (
  <svg viewBox="0 0 64 64" className={className}>
    <g {...stroke}>
      <circle cx="23" cy="24" r="9" />
      <path d="M29 30 L46 47" />
      <path d="M39 40 l6 -6" />
      <path d="M44 45 l6 -6" />
    </g>
  </svg>
)

const Glasses: Art = ({ className }) => (
  <svg viewBox="0 0 64 64" className={className}>
    <g {...stroke}>
      <circle cx="19" cy="34" r="9" />
      <circle cx="45" cy="34" r="9" />
      <path d="M28 32 q4 -4 8 0" />
      <path d="M10 30 l-4 -4" opacity={0.55} />
      <path d="M54 30 l4 -4" opacity={0.55} />
    </g>
  </svg>
)

const Book: Art = ({ className }) => (
  <svg viewBox="0 0 64 64" className={className}>
    <g {...stroke}>
      <path d="M14 16 h16 a6 6 0 0 1 6 6 v28 a6 6 0 0 0 -6 -5 h-16 z" />
      <path d="M50 16 h-14 a6 6 0 0 0 -6 6 v28 a6 6 0 0 1 6 -5 h14 z" />
    </g>
  </svg>
)

/** Anything we do not have specific art for. */
const Package: Art = ({ className }) => (
  <svg viewBox="0 0 64 64" className={className}>
    <g {...stroke}>
      <path d="M32 12 l20 10 v20 l-20 10 -20 -10 v-20 z" />
      <path d="M12 22 l20 10 20 -10" />
      <path d="M32 32 v20" opacity={0.45} />
    </g>
  </svg>
)

/** Matched against the item type first, then the category. Longest keys first. */
const BY_KEYWORD: Array<[string, Art]> = [
  ['backpack', Backpack],
  ['laptop bag', Backpack],
  ['laptop', Laptop],
  ['headphone', Headphones],
  ['earphone', Headphones],
  ['phone', Phone],
  ['wallet', Wallet],
  ['purse', Wallet],
  ['bag', Backpack],
  ['key', Keys],
  ['glass', Glasses],
  ['book', Book],
  ['stationery', Book],
  ['document', Book],
  ['card', Wallet],
  ['electronic', Phone],
  ['clothing', Package],
  ['jewel', Package],
]

/** Internal: keeping this unexported means the file only exports components, so Fast Refresh works. */
function illustrationFor(itemType: string, category: string): Art {
  const haystack = `${itemType} ${category}`.toLowerCase()
  return BY_KEYWORD.find(([keyword]) => haystack.includes(keyword))?.[1] ?? Package
}

export function ItemIllustration({
  itemType,
  category,
  className,
}: {
  itemType: string
  category: string
  className?: string
}) {
  const Art = illustrationFor(itemType, category)
  return <Art className={cn('size-full', className)} />
}

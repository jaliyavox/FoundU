import { useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { ArrowRightIcon, ChevronLeftIcon, ChevronRightIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { useAuth } from '@/features/auth/use-auth'
import { cn } from '@/lib/utils'
import { getFoundFeed, type FoundPostItem } from './feed-api'
import { FlameIcon } from './flame-icon'
import { FoundPostCard, FoundPostPanel } from './found-feed'

/** Enough to feel like a row without turning the top of the page into a second feed. */
const STRIP_SIZE = 10

/**
 * The newest things people have picked up, across the top of the lost board.
 *
 * It sits above the reports rather than behind a "Found" tab because the two boards answer
 * different questions and only one of them is time-critical: an item somebody is holding
 * right now is worth seeing before you scroll. The full board is one button away.
 */
export function FoundStrip() {
  const { user } = useAuth()
  const scroller = useRef<HTMLUListElement>(null)
  const [selected, setSelected] = useState<FoundPostItem | null>(null)

  const { data, isPending, isError } = useQuery({
    queryKey: ['found-strip', { viewer: user?.id ?? null }],
    queryFn: () => getFoundFeed({ page: 1, pageSize: STRIP_SIZE }),
  })

  // A strip nobody can act on is noise above the board people came for: when it is empty or
  // the call failed, the page reads as if it were never there. The full board still says so.
  if (isError || (!isPending && data.items.length === 0)) return null

  const scrollBy = (direction: 1 | -1) =>
    scroller.current?.scrollBy({ left: direction * (scroller.current.clientWidth * 0.8), behavior: 'smooth' })

  return (
    <section
      aria-labelledby="fresh-finds"
      /* Its own light panel: the row sits on the same mist as the feed, lifted just enough
         that the dark cards read as resting on something rather than floating. */
      className="mb-10 rounded-3xl bg-linear-to-b from-white via-white/70 to-brand-mist/30 p-5 ring-1 ring-brand-forest/8 sm:p-6"
    >
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div className="flex items-start gap-3">
          <span
            aria-hidden="true"
            className="flex size-10 shrink-0 items-center justify-center rounded-2xl bg-linear-to-b from-amber-50 to-orange-100/60 ring-1 ring-orange-500/10"
          >
            <FlameIcon className="size-6" />
          </span>
          <div>
            <h2 id="fresh-finds" className="text-2xl font-semibold tracking-tight text-neutral-900">
              Fresh finds
            </h2>
            <p className="pt-1 text-sm text-neutral-600">
              I found this just now - who owns this?
            </p>
          </div>
        </div>

        <div className="flex items-center gap-2">
          {/* Arrows only earn their place once the row actually overflows. */}
          <div className={cn('hidden gap-1', !isPending && data.items.length > 3 && 'sm:flex')}>
            <Button
              variant="outline"
              size="icon"
              aria-label="Scroll to earlier finds"
              onClick={() => scrollBy(-1)}
              className="rounded-full border-brand-forest/20 text-brand-forest hover:bg-brand-forest/10 hover:text-brand-forest"
            >
              <ChevronLeftIcon aria-hidden="true" />
            </Button>
            <Button
              variant="outline"
              size="icon"
              aria-label="Scroll to later finds"
              onClick={() => scrollBy(1)}
              className="rounded-full border-brand-forest/20 text-brand-forest hover:bg-brand-forest/10 hover:text-brand-forest"
            >
              <ChevronRightIcon aria-hidden="true" />
            </Button>
          </div>
          <Button
            variant="ghost"
            nativeButton={false}
            render={<Link to="/found" />}
            className="text-brand-forest hover:bg-brand-forest/8"
          >
            See more<ArrowRightIcon aria-hidden="true" />
          </Button>
        </div>
      </div>

      {/* Scrollable with a mouse, a trackpad, or the arrow keys once the list has focus. */}
      <ul
        ref={scroller}
        tabIndex={0}
        aria-label="Recently found items"
        className="mt-5 flex snap-x snap-mandatory gap-4 overflow-x-auto pb-2 [scrollbar-width:thin] focus-visible:ring-2 focus-visible:ring-brand-forest focus-visible:outline-none"
      >
        {isPending
          ? Array.from({ length: 4 }).map((_, index) => (
              <li key={index} className="h-64 w-64 shrink-0 animate-pulse rounded-2xl bg-brand-forest/10" />
            ))
          : data.items.map(item => (
              <li key={item.id} className="w-64 shrink-0 snap-start">
                <FoundPostCard item={item} onOpen={() => setSelected(item)} />
              </li>
            ))}
      </ul>

      <FoundPostPanel item={selected} onClose={() => setSelected(null)} />
    </section>
  )
}

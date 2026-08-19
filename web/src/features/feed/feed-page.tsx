import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import {
  ChevronLeftIcon,
  ChevronRightIcon,
  PlusIcon,
  RotateCwIcon,
  SearchIcon,
  SearchXIcon,
} from 'lucide-react'
import { GradientDivider } from '@/components/landing/bento'
import { SiteNav } from '@/components/landing/site-nav'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { useAuth } from '@/features/auth/use-auth'
import { getFeed, type LostReportFeedItem } from './feed-api'
import { FeedCard } from './feed-card'
import { CardConnector } from './card-connector'
import { FeedDetailPanel } from './feed-detail-panel'
import { FeedSpotlight } from './feed-spotlight'
import { ApiError } from '@/lib/api/client'
import { cn } from '@/lib/utils'
import { homeRouteForRole } from '@/routes/role-home'

const PAGE_SIZE = 9

const PUBLIC_LINKS = [
  { href: '/', label: 'Home' },
  { href: '/#how-it-works', label: 'How it works' },
  { href: '/#faq', label: 'FAQ' },
]

export function FeedPage() {
  const { user } = useAuth()
  const [page, setPage] = useState(1)
  const [selected, setSelected] = useState<LostReportFeedItem | null>(null)
  const [showHandIn, setShowHandIn] = useState(false)
  const [zoomed, setZoomed] = useState(false)
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')

  const { data, isPending, isError, error, isFetching, refetch } = useQuery({
    // The signed-in user is part of the key because the response is: `isMine` is computed
    // per caller, so a cached anonymous page must not survive a sign-in.
    queryKey: ['lost-feed', { page, search, viewer: user?.id ?? null }],
    queryFn: () => getFeed({ page, pageSize: PAGE_SIZE, search }),
    // Keeps the previous page on screen while the next one loads, instead of flashing skeletons.
    placeholderData: keepPreviousData,
  })

  function handleSearch(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setPage(1)
    setSearch(searchInput)
  }

  // Students go straight to the form; staff have no lost-report form, so they land on
  // their own workspace instead.
  const postHref = !user ? '/login' : user.role === 'Student' ? '/my-reports/new' : homeRouteForRole(user.role)
  const postLabel = user ? 'Post a lost item' : 'Sign in to post'

  return (
    <div className="flex min-h-svh flex-col bg-[oklch(0.17_0.028_148)] text-white">
      {/* The dashboard is the CTA button - listing it in the links as well showed it twice. */}
      <SiteNav
        ctaHref={user ? homeRouteForRole(user.role) : '/login'}
        ctaLabel={user ? 'Dashboard' : 'Sign in'}
        links={PUBLIC_LINKS}
      />

      <main className="flex-1">
        <section className="relative isolate overflow-hidden pt-32 pb-10 sm:pt-36">
          <div aria-hidden="true" className="pointer-events-none absolute inset-0 -z-10">
            <div className="fu-aurora-a absolute -top-40 left-[10%] size-[34rem] rounded-full bg-brand-forest/60 blur-[130px]" />
            <div className="fu-aurora-b absolute -top-20 right-[5%] size-[26rem] rounded-full bg-brand-green/25 blur-[130px]" />
          </div>

          <div className="mx-auto w-full max-w-5xl px-6">
            <p className="text-sm font-medium text-brand-green">Lost feed</p>

            <h1 className="pt-3 text-4xl font-semibold tracking-tight text-balance sm:text-5xl">
              What people are looking for
            </h1>
            <p className="max-w-xl pt-3 text-sm text-pretty text-white/60 sm:text-base">
              Every open report from across campus. Recognise something? Hand it in at the nearest
              desk and we will get it back to them.
            </p>

            <div className="flex flex-col gap-3 pt-8 sm:flex-row sm:items-center">
              <form onSubmit={handleSearch} className="flex flex-1 items-center gap-2">
                <div className="relative flex-1">
                  <Label htmlFor="feed-search" className="sr-only">
                    Search lost items
                  </Label>
                  <SearchIcon
                    className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-white/40"
                    aria-hidden="true"
                  />
                  <Input
                    id="feed-search"
                    value={searchInput}
                    onChange={(event) => setSearchInput(event.target.value)}
                    placeholder="Search by item, colour or place"
                    className="border-white/12 bg-white/[0.06] pl-9 text-white placeholder:text-white/35"
                  />
                </div>
                <Button
                  type="submit"
                  variant="outline"
                  className="border-white/20 bg-white/8 text-white hover:bg-white/15 hover:text-white"
                >
                  Search
                </Button>
              </form>

              <Button
                className="group rounded-xl bg-white text-brand-forest hover:bg-white/90"
                nativeButton={false}
                render={<Link to={postHref} />}
              >
                <PlusIcon aria-hidden="true" />
                {postLabel}
              </Button>
            </div>
          </div>
        </section>

        <GradientDivider />

        <section
          aria-label="Lost item reports"
          className="relative overflow-hidden bg-brand-mist"
        >
          {/* Same light ground as the FAQ band, so the dark cards read as raised panels. */}
          <div aria-hidden="true" className="pointer-events-none absolute inset-0 -z-10">
            <div
              className="absolute inset-0"
              style={{
                background:
                  'radial-gradient(ellipse 70% 80% at 50% 0%, rgba(255,255,255,0.95), transparent 70%)',
              }}
            />
            <div className="fu-aurora-c absolute right-[6%] -bottom-32 size-96 rounded-full bg-brand-sage/35 blur-3xl" />
          </div>

          <div className="relative mx-auto w-full max-w-5xl px-6 py-12">
          {isPending ? (
            <FeedSkeleton />
          ) : isError ? (
            <FeedError error={error} onRetry={() => refetch()} />
          ) : data.items.length === 0 ? (
            <FeedEmpty
              search={search}
              onClear={() => {
                setSearchInput('')
                setSearch('')
                setPage(1)
              }}
            />
          ) : (
            <>
              <p className="pb-5 text-xs text-brand-forest/60">
                {data.totalCount} open {data.totalCount === 1 ? 'report' : 'reports'}
                {search && ` matching “${search}”`}
              </p>

              <ul
                className={cn(
                  'grid gap-5 transition-opacity duration-200 sm:grid-cols-2 lg:grid-cols-3',
                  isFetching && 'opacity-60',
                )}
              >
                {data.items.map((item) => (
                  <li key={item.id}>
                    <FeedCard
                      item={item}
                      isActive={selected?.id === item.id}
                      onOpen={() => {
                        setSelected(item)
                        setShowHandIn(false)
                        setZoomed(false)
                      }}
                    />
                  </li>
                ))}
              </ul>

              {data.totalPages > 1 && (
                <nav
                  aria-label="Feed pages"
                  className="flex items-center justify-between gap-4 pt-8"
                >
                  <Button
                    variant="outline"
                    disabled={!data.hasPreviousPage}
                    onClick={() => setPage((p) => Math.max(1, p - 1))}
                    className="border-brand-forest/15 bg-white/80 text-brand-forest hover:bg-white"
                  >
                    <ChevronLeftIcon aria-hidden="true" />
                    Previous
                  </Button>

                  <span className="text-xs text-brand-forest/60 tabular-nums">
                    Page {data.page} of {data.totalPages}
                  </span>

                  <Button
                    variant="outline"
                    disabled={!data.hasNextPage}
                    onClick={() => setPage((p) => p + 1)}
                    className="border-brand-forest/15 bg-white/80 text-brand-forest hover:bg-white"
                  >
                    Next
                    <ChevronRightIcon aria-hidden="true" />
                  </Button>
                </nav>
              )}
            </>
          )}
          </div>
        </section>
      </main>

      <FeedDetailPanel
        item={selected}
        showHandIn={showHandIn}
        onZoom={() => setZoomed(true)}
        onShowHandIn={setShowHandIn}
        onClose={() => {
          setSelected(null)
          setZoomed(false)
        }}
      />
      <FeedSpotlight
        item={selected}
        showHandIn={showHandIn}
        zoomed={zoomed}
        onCloseZoom={() => setZoomed(false)}
      />
      <CardConnector cardId={selected?.id ?? null} showHandIn={showHandIn} />
    </div>
  )
}

/* -------------------------------------------------------------------------- */

function FeedSkeleton() {
  return (
    <ul className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3" aria-label="Loading reports">
      {Array.from({ length: 6 }).map((_, index) => (
        <li key={index} className="overflow-hidden rounded-2xl bg-[oklch(0.21_0.03_148)] ring-1 ring-white/8">
          <Skeleton className="aspect-4/3 rounded-none bg-white/6" />
          <div className="flex flex-col gap-2 p-5">
            <Skeleton className="h-5 w-28 bg-white/10" />
            <Skeleton className="h-3.5 w-36 bg-white/8" />
            <div className="space-y-2 pt-1">
              <Skeleton className="h-3.5 w-full bg-white/8" />
              <Skeleton className="h-3.5 w-4/5 bg-white/8" />
            </div>
            <Skeleton className="mt-3 h-3 w-32 bg-white/8" />
          </div>
        </li>
      ))}
    </ul>
  )
}

function FeedEmpty({ search, onClear }: { search: string; onClear: () => void }) {
  return (
    <div className="flex flex-col items-center gap-3 rounded-2xl border border-brand-forest/10 bg-white/70 px-6 py-16 text-center">
      <span className="flex size-12 items-center justify-center rounded-2xl border border-brand-forest/10 bg-brand-mist">
        <SearchXIcon className="size-5 text-brand-forest/50" aria-hidden="true" />
      </span>
      <p className="text-base font-medium text-brand-forest">
        {search ? `Nothing matching “${search}”` : 'No open reports right now'}
      </p>
      <p className="max-w-sm text-sm text-brand-forest/65">
        {search
          ? 'Try a broader term - an item type like “backpack”, a colour, or a building.'
          : 'When someone reports something lost, it will appear here.'}
      </p>
      {search && (
        <Button
          variant="outline"
          onClick={onClear}
          className="mt-1 border-white/15 bg-white/[0.06] text-white hover:bg-white/12 hover:text-white"
        >
          Clear search
        </Button>
      )}
    </div>
  )
}

function FeedError({ error, onRetry }: { error: unknown; onRetry: () => void }) {
  const message =
    error instanceof ApiError
      ? error.message
      : 'Could not reach the server. Check the API is running.'

  return (
    <div
      role="alert"
      className="flex flex-col items-center gap-3 rounded-2xl border border-destructive/30 bg-destructive/5 px-6 py-16 text-center"
    >
      <p className="text-base font-medium text-brand-forest">The feed could not be loaded</p>
      <p className="max-w-sm text-sm text-brand-forest/70">{message}</p>
      <Button
        variant="outline"
        onClick={onRetry}
        className="mt-1 border-white/15 bg-white/[0.06] text-white hover:bg-white/12 hover:text-white"
      >
        <RotateCwIcon aria-hidden="true" />
        Try again
      </Button>
    </div>
  )
}

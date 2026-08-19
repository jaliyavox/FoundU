import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import {
  ChevronLeftIcon,
  ChevronRightIcon,
  ClockIcon,
  MapPinIcon,
  PlusIcon,
  RotateCwIcon,
  SearchIcon,
  SearchXIcon,
} from 'lucide-react'
import { GradientDivider } from '@/components/landing/bento'
import { SiteNav } from '@/components/landing/site-nav'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { useAuth } from '@/features/auth/use-auth'
import { formatWindow, getFeed, timeAgo, type LostReportFeedItem } from './feed-api'
import { ApiError } from '@/lib/api/client'
import { cn } from '@/lib/utils'
import { homeRouteForRole } from '@/routes/role-home'

const PAGE_SIZE = 9

const NAV_LINKS = [
  { href: '/', label: 'Home' },
  { href: '/#how-it-works', label: 'How it works' },
  { href: '/#faq', label: 'FAQ' },
]

export function FeedPage() {
  const { user } = useAuth()
  const [page, setPage] = useState(1)
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')

  const { data, isPending, isError, error, isFetching, refetch } = useQuery({
    queryKey: ['lost-feed', { page, search }],
    queryFn: () => getFeed({ page, pageSize: PAGE_SIZE, search }),
    // Keeps the previous page on screen while the next one loads, instead of flashing skeletons.
    placeholderData: keepPreviousData,
  })

  function handleSearch(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setPage(1)
    setSearch(searchInput)
  }

  // Anonymous visitors are sent to sign in; the create form itself lands with Step 6's UI.
  const postHref = user ? homeRouteForRole(user.role) : '/login'
  const postLabel = user ? 'Post a lost item' : 'Sign in to post'

  return (
    <div className="flex min-h-svh flex-col bg-[oklch(0.17_0.028_148)] text-white">
      <SiteNav ctaHref={user ? homeRouteForRole(user.role) : '/login'} ctaLabel={user ? 'Dashboard' : 'Sign in'} links={NAV_LINKS} />

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

        <section aria-label="Lost item reports" className="mx-auto w-full max-w-5xl px-6 py-10">
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
              <p className="pb-5 text-xs text-white/40">
                {data.totalCount} open {data.totalCount === 1 ? 'report' : 'reports'}
                {search && ` matching “${search}”`}
              </p>

              <ul
                className={cn(
                  'grid gap-4 transition-opacity duration-200 sm:grid-cols-2 lg:grid-cols-3',
                  isFetching && 'opacity-60',
                )}
              >
                {data.items.map((item) => (
                  <li key={item.id}>
                    <FeedCard item={item} />
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
                    className="border-white/15 bg-white/[0.06] text-white hover:bg-white/12 hover:text-white"
                  >
                    <ChevronLeftIcon aria-hidden="true" />
                    Previous
                  </Button>

                  <span className="text-xs text-white/45 tabular-nums">
                    Page {data.page} of {data.totalPages}
                  </span>

                  <Button
                    variant="outline"
                    disabled={!data.hasNextPage}
                    onClick={() => setPage((p) => p + 1)}
                    className="border-white/15 bg-white/[0.06] text-white hover:bg-white/12 hover:text-white"
                  >
                    Next
                    <ChevronRightIcon aria-hidden="true" />
                  </Button>
                </nav>
              )}
            </>
          )}
        </section>
      </main>
    </div>
  )
}

/* -------------------------------------------------------------------------- */

function FeedCard({ item }: { item: LostReportFeedItem }) {
  const initials = item.postedByName
    .trim()
    .split(/\s+/)
    .map((part) => part[0])
    .slice(0, 2)
    .join('')
    .toUpperCase()

  return (
    <article className="group flex h-full flex-col rounded-2xl border border-white/10 bg-linear-to-b from-white/[0.07] to-white/[0.015] p-5 transition-all duration-300 hover:-translate-y-0.5 hover:border-brand-green/35">
      <header className="flex items-center gap-3">
        <span className="flex size-9 shrink-0 items-center justify-center rounded-full border border-white/12 bg-brand-green/15 text-xs font-medium text-brand-green">
          {initials}
        </span>
        <div className="min-w-0 flex-1 leading-tight">
          <p className="truncate text-sm font-medium text-white/90">{item.postedByName}</p>
          <p className="text-xs text-white/40">{timeAgo(item.createdAt)}</p>
        </div>
      </header>

      <div className="flex flex-wrap gap-1.5 pt-4">
        <Badge variant="secondary" className="bg-white/[0.08] text-white/75">
          {item.itemTypeName}
        </Badge>
        {item.primaryColor && (
          <Badge variant="secondary" className="bg-white/[0.08] text-white/75">
            {item.primaryColor}
          </Badge>
        )}
      </div>

      <p className="flex-1 pt-3 text-sm text-pretty text-white/65">{item.description}</p>

      <footer className="flex flex-col gap-1.5 pt-4 text-xs text-white/45">
        <span className="flex items-center gap-1.5">
          <MapPinIcon className="size-3.5 shrink-0 text-brand-green/70" aria-hidden="true" />
          <span className="truncate">{item.lastSeenLocationName}</span>
        </span>
        <span className="flex items-center gap-1.5">
          <ClockIcon className="size-3.5 shrink-0 text-brand-green/70" aria-hidden="true" />
          {formatWindow(item.estimatedLostFromAt, item.estimatedLostToAt)}
        </span>
      </footer>
    </article>
  )
}

function FeedSkeleton() {
  return (
    <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3" aria-label="Loading reports">
      {Array.from({ length: 6 }).map((_, index) => (
        <li key={index} className="rounded-2xl border border-white/10 bg-white/[0.03] p-5">
          <div className="flex items-center gap-3">
            <Skeleton className="size-9 rounded-full bg-white/10" />
            <div className="flex-1 space-y-1.5">
              <Skeleton className="h-3 w-24 bg-white/10" />
              <Skeleton className="h-2.5 w-16 bg-white/10" />
            </div>
          </div>
          <Skeleton className="mt-4 h-5 w-20 rounded-full bg-white/10" />
          <div className="mt-4 space-y-2">
            <Skeleton className="h-3 w-full bg-white/10" />
            <Skeleton className="h-3 w-4/5 bg-white/10" />
          </div>
          <Skeleton className="mt-5 h-3 w-32 bg-white/10" />
        </li>
      ))}
    </ul>
  )
}

function FeedEmpty({ search, onClear }: { search: string; onClear: () => void }) {
  return (
    <div className="flex flex-col items-center gap-3 rounded-2xl border border-white/10 bg-white/[0.03] px-6 py-16 text-center">
      <span className="flex size-12 items-center justify-center rounded-2xl border border-white/12 bg-white/[0.05]">
        <SearchXIcon className="size-5 text-white/40" aria-hidden="true" />
      </span>
      <p className="text-base font-medium text-white/85">
        {search ? `Nothing matching “${search}”` : 'No open reports right now'}
      </p>
      <p className="max-w-sm text-sm text-white/50">
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
      className="flex flex-col items-center gap-3 rounded-2xl border border-destructive/30 bg-destructive/10 px-6 py-16 text-center"
    >
      <p className="text-base font-medium text-white/90">The feed could not be loaded</p>
      <p className="max-w-sm text-sm text-white/60">{message}</p>
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

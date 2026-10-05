import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ClockIcon, HashIcon, HandIcon, Loader2Icon, MapPinIcon, Trash2Icon } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from '@/components/ui/sheet'
import { useAuth } from '@/features/auth/use-auth'
import { ApiError } from '@/lib/api/client'
import { cn } from '@/lib/utils'
import {
  declareFoundPostHandedIn,
  displayCode,
  getFoundFeed,
  invalidateFoundPosts,
  timeAgo,
  withdrawFoundPost,
  type FoundPostItem,
  postStage,
  STAGE_BADGE,
  STAGE_NOTE,
} from './feed-api'
import { ItemIllustration } from './item-illustration'
import { MessageThread } from './message-thread'
import { CardConnector } from './card-connector'
import { FoundSpotlight } from './found-spotlight'

const PAGE_SIZE = 12

/**
 * The other half of the feed: things students have found and not yet walked to a desk.
 *
 * Every card is a teaser - what it is, where, when, in the finder's words - and nothing that
 * proves ownership. An owner who recognises theirs says so; the finder is told to hand it in;
 * the desk's questions still decide. Nobody can claim from here.
 */
export function FoundFeed({ search }: { search: string }) {
  const { user } = useAuth()
  const [page, setPage] = useState(1)
  const [selected, setSelected] = useState<FoundPostItem | null>(null)

  const { data, isPending, isError, isFetching } = useQuery({
    queryKey: ['found-feed', { page, search, viewer: user?.id ?? null }],
    queryFn: () => getFoundFeed({ page, pageSize: PAGE_SIZE, search }),
    placeholderData: keepPreviousData,
  })

  if (isPending) {
    return (
      <ul className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
        {Array.from({ length: 6 }).map((_, i) => (
          <li key={i} className="h-72 animate-pulse rounded-2xl bg-brand-forest/10" />
        ))}
      </ul>
    )
  }

  if (isError) {
    return <p className="text-sm text-brand-forest">Could not load found items. Check the API is running.</p>
  }

  if (data.items.length === 0) {
    return (
      <div className="flex flex-col items-start gap-2 py-8">
        <p className="font-medium text-neutral-900">Nothing posted yet</p>
        <p className="max-w-md text-sm text-neutral-600">
          When a student finds something and posts it here, it shows until they hand it in at a
          desk. Found something yourself?{' '}
          <Link to={user ? '/found/new' : '/login'} className="font-medium text-brand-forest underline underline-offset-4">
            Post it
          </Link>
          .
        </p>
      </div>
    )
  }

  return (
    <>
      <p className="pb-5 text-xs text-brand-forest">
        {data.totalCount} {data.totalCount === 1 ? 'item' : 'items'} waiting for {data.totalCount === 1 ? 'its owner' : 'their owners'}
        {search && ` matching “${search}”`}
      </p>

      <ul className={cn('grid gap-5 transition-opacity duration-200 sm:grid-cols-2 lg:grid-cols-3', isFetching && 'opacity-60')}>
        {data.items.map((item) => (
          <li key={item.id}>
            <FoundPostCard item={item} onOpen={() => setSelected(item)} />
          </li>
        ))}
      </ul>

      {data.totalPages > 1 && (
        <nav aria-label="Found item pages" className="flex items-center justify-between pt-8">
          <Button variant="outline" disabled={!data.hasPreviousPage} onClick={() => setPage((p) => p - 1)}>
            Previous
          </Button>
          <span className="text-xs text-brand-forest tabular-nums">
            Page {data.page} of {data.totalPages}
          </span>
          <Button variant="outline" disabled={!data.hasNextPage} onClick={() => setPage((p) => p + 1)}>
            Next
          </Button>
        </nav>
      )}

      <FoundPostPanel item={selected} onClose={() => setSelected(null)} />
    </>
  )
}

export function FoundPostCard({ item, onOpen }: { item: FoundPostItem; onOpen: () => void }) {
  const meta = [item.primaryColor, item.foundLocationName].filter(Boolean).join(' · ')

  return (
    <button
      type="button"
      onClick={onOpen}
      aria-label={`${item.itemTypeName} found at ${item.foundLocationName}. Open details.`}
      className="group relative flex h-full w-full flex-col overflow-hidden rounded-2xl bg-[oklch(0.21_0.03_148)] text-left ring-1 ring-white/8 transition duration-300 hover:-translate-y-1 hover:ring-white/20 hover:shadow-xl hover:shadow-brand-forest/25 focus-visible:ring-2 focus-visible:ring-brand-green focus-visible:outline-none"
    >
      <div className="relative aspect-4/3 overflow-hidden bg-[oklch(0.25_0.035_148)]">
        <ItemIllustration
          itemType={item.itemTypeName}
          category={item.categoryName}
          className="absolute inset-0 m-auto size-20 text-brand-sage/70 transition-transform duration-300 group-hover:scale-105"
        />
        {/* Says plainly where the item is: with the finder, at a desk, or spoken for. */}
        <span className={cn('absolute top-3 left-3 rounded-full px-2.5 py-1 text-[11px] font-semibold', STAGE_BADGE[postStage(item).tone])}>
          {postStage(item).badge}
        </span>
      </div>

      <div className="flex flex-1 flex-col gap-2 p-5">
        <h3 className="text-lg leading-snug font-medium text-white">{item.itemTypeName}</h3>
        {meta && <p className="text-sm text-white/55">{meta}</p>}
        <p className="line-clamp-2 flex-1 text-sm leading-relaxed text-pretty text-white/70">{item.description}</p>
        <div className="flex items-center gap-2 pt-3 text-xs text-white/65">
          <HandIcon className="size-3.5 text-brand-green" aria-hidden="true" />
          <span className="truncate">Found by {item.isMine ? 'you' : item.postedByName}</span>
          <span aria-hidden="true">·</span>
          <span className="shrink-0">{timeAgo(item.createdAt)}</span>
        </div>
      </div>
    </button>
  )
}

/** Detail, and the two things a person can do: recognise it as theirs, or take down their own post. */
export function FoundPostPanel({ item, onClose }: { item: FoundPostItem | null; onClose: () => void }) {
  const { user } = useAuth()
  const queryClient = useQueryClient()
  const [selectedPostState, setSelectedPostState] = useState<FoundPostItem | null>(item)

  useEffect(() => {
    setSelectedPostState(item)
  }, [item])

  // Spoken for once a claim is approved - nobody else can say it is theirs then.
  const canRecognise = user?.role === 'Student' && item !== null && !item.isMine && item.status !== 'Claimed'

  const withdraw = useMutation({
    mutationFn: () => withdrawFoundPost(item!.id),
    onSuccess: () => {
      invalidateFoundPosts(queryClient)
      toast.success('Post taken down.')
      onClose()
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : 'Could not reach the server.'),
  })

  const handIn = useMutation({
    mutationFn: () => declareFoundPostHandedIn(item!.id),
    onSuccess: next => {
      setSelectedPostState(next)
      invalidateFoundPosts(queryClient)
      toast.success('Saved. Security still needs to confirm receipt at the desk.')
    },
    onError: error => toast.error(error instanceof ApiError ? error.message : 'Could not update the post.'),
  })

  const displayedItem = selectedPostState ?? item

  return (
    <>
      {/* Same lift-and-chain as the lost board: card out of the grid, dashed line, panel. */}
      <FoundSpotlight item={item} />
      <CardConnector cardId={item?.id ?? null} viaMiddle={false} />

      <Sheet
      open={item !== null}
      onOpenChange={(open) => {
        if (!open) {
          onClose()
        }
      }}
    >
      <SheetContent className="w-full gap-0 overflow-y-auto border-brand-forest/10 bg-linear-to-b from-white to-brand-mist text-neutral-900 sm:max-w-md">
        {item && (
          <div className="flex flex-col gap-5 p-6">
            <SheetHeader className="gap-1 p-0">
              <SheetTitle className="text-xl text-neutral-900">{item.itemTypeName}</SheetTitle>
              <SheetDescription className="text-neutral-500">
                Found by {item.isMine ? 'you' : item.postedByName} · {timeAgo(item.createdAt)}
              </SheetDescription>
            </SheetHeader>

            <p className={cn('rounded-xl border px-3 py-2 text-sm', STAGE_NOTE[postStage(displayedItem ?? item).tone])}>
              {postStage(displayedItem ?? item).note}
            </p>

            <p className="text-sm leading-relaxed text-pretty text-neutral-700">{item.description}</p>

            <dl className="flex flex-col gap-3 rounded-xl border border-neutral-900/8 bg-white/70 p-4">
              <div className="flex items-start gap-3">
                <MapPinIcon className="mt-0.5 size-4 shrink-0 text-brand-green" aria-hidden="true" />
                <div>
                  <dt className="text-xs text-neutral-500">Found at</dt>
                  <dd className="text-sm">{item.foundLocationName}</dd>
                </div>
              </div>
              <div className="flex items-start gap-3">
                <ClockIcon className="mt-0.5 size-4 shrink-0 text-brand-green" aria-hidden="true" />
                <div>
                  <dt className="text-xs text-neutral-500">When</dt>
                  <dd className="text-sm">
                    {new Date(item.foundAt).toLocaleString('en', { weekday: 'short', day: 'numeric', month: 'short', hour: 'numeric', minute: '2-digit' })}
                  </dd>
                </div>
              </div>
            </dl>

            {item.isMine && displayedItem?.handInCode && (
              <div className="flex items-center justify-between gap-4 rounded-xl bg-brand-forest px-4 py-3 text-white">
                <div>
                  <p className="text-xs text-white/70">Quote this at the desk when you hand it in</p>
                  <p className="font-mono text-2xl font-semibold tracking-[0.2em] tabular-nums">{displayCode(displayedItem.handInCode)}</p>
                </div>
                <HashIcon className="size-6 shrink-0 text-white/60" aria-hidden="true" />
              </div>
            )}

            {item.isMine && (
              <div className="flex flex-col gap-3 border-t border-neutral-900/8 pt-5">
                <p className="text-sm font-medium">People asking about this</p>
                {item.status === 'Posted' && item.canMessageFinder !== false && <MessageThread reportId={item.id} isAuthor source="found" tone="light" />}
              </div>
            )}

            {item.isMine ? (
              <div className="flex flex-col gap-3">
                {displayedItem?.status === 'Posted' && !displayedItem.handedToSecurityAt && (
                  <Button
                    className="bg-brand-forest text-white hover:bg-brand-forest/90"
                    onClick={() => handIn.mutate()}
                    disabled={handIn.isPending}
                  >
                    {handIn.isPending ? <Loader2Icon className="animate-spin" aria-hidden="true" /> : <HandIcon aria-hidden="true" />}
                    I gave it to security
                  </Button>
                )}
                {/* Once a desk has it, it is the desk's to deal with - not the finder's to take down. */}
                {displayedItem?.status === 'Posted' && (
                <Button
                  variant="outline"
                  className="border-neutral-900/15 bg-white/70 text-neutral-800 hover:bg-white"
                  onClick={() => withdraw.mutate()}
                  disabled={withdraw.isPending || handIn.isPending}
                >
                  {withdraw.isPending ? <Loader2Icon className="animate-spin" aria-hidden="true" /> : <Trash2Icon aria-hidden="true" />}
                  Take this post down
                </Button>
                )}
              </div>
            ) : !user ? (
              <Button className="bg-brand-forest text-white hover:bg-brand-forest/90" nativeButton={false} render={<Link to="/login" />}>
                Sign in if this is yours
              </Button>
            ) : canRecognise ? (
              <div className="flex flex-col gap-4 border-t border-neutral-900/8 pt-5">
                {/* Asking comes first and needs no report: a question is not a claim, and
                    making someone post a lost report before they can ask "does it have a
                    dent in the lid?" is a wall in front of the obvious first step. */}
                <div>
                  <p className="text-sm font-medium">{item.status === 'Unclaimed' ? `This item is now held at ${item.storageLocationName ?? 'the security desk'}. Submit a claim to verify ownership.` : 'Think it is yours?'}</p>
                  {item.status === 'Posted' && item.canMessageFinder !== false && <p className="text-xs text-neutral-500">
                    Ask {item.postedByName.split(' ')[0]} about it - a detail only the owner
                    would know is the quickest way to be sure. Nothing is claimed by asking.
                  </p>}
                </div>

                {item.status === 'Posted' && item.canMessageFinder !== false && <MessageThread reportId={item.id} isAuthor={false} source="found" tone="light" />}

                <Link to="/my-reports" className="font-medium text-brand-forest underline underline-offset-4">
                  Open your matches to submit a claim
                </Link>
              </div>
            ) : (
              <p className="text-sm text-neutral-500">Staff can pull this post up at the desk by the finder's code.</p>
            )}
          </div>
        )}
      </SheetContent>
      </Sheet>
    </>
  )
}

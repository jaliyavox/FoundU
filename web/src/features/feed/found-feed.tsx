import { useState } from 'react'
import { Link } from 'react-router-dom'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ClockIcon, HashIcon, HandIcon, Loader2Icon, MapPinIcon, Trash2Icon } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from '@/components/ui/sheet'
import { useAuth } from '@/features/auth/use-auth'
import { getMyLostReports } from '@/features/reports/reports-api'
import { FormSelect } from '@/features/reports/form-select'
import { ApiError } from '@/lib/api/client'
import { cn } from '@/lib/utils'
import { displayCode, getFoundFeed, recogniseFoundPost, timeAgo, withdrawFoundPost, type FoundPostItem } from './feed-api'
import { ItemIllustration } from './item-illustration'

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
    return <p className="text-sm text-brand-forest/70">Could not load found items. Check the API is running.</p>
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
      <p className="pb-5 text-xs text-brand-forest/60">
        {data.totalCount} {data.totalCount === 1 ? 'item' : 'items'} waiting to reach a desk
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
          <span className="text-xs text-brand-forest/60 tabular-nums">
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

function FoundPostCard({ item, onOpen }: { item: FoundPostItem; onOpen: () => void }) {
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
        {/* Says plainly what a teaser is: not yet in anyone's custody. */}
        <span className="absolute top-3 left-3 rounded-full bg-amber-400/90 px-2.5 py-1 text-[11px] font-semibold text-neutral-900">
          {item.isMine ? 'Your post' : 'Not at a desk yet'}
        </span>
      </div>

      <div className="flex flex-1 flex-col gap-2 p-5">
        <h3 className="text-lg leading-snug font-medium text-white">{item.itemTypeName}</h3>
        {meta && <p className="text-sm text-white/55">{meta}</p>}
        <p className="line-clamp-2 flex-1 text-sm leading-relaxed text-pretty text-white/70">{item.description}</p>
        <div className="flex items-center gap-2 pt-3 text-xs text-white/40">
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
function FoundPostPanel({ item, onClose }: { item: FoundPostItem | null; onClose: () => void }) {
  const { user } = useAuth()
  const queryClient = useQueryClient()
  const [reportId, setReportId] = useState('')
  const [done, setDone] = useState(false)

  const canRecognise = user?.role === 'Student' && item !== null && !item.isMine

  const myReports = useQuery({
    queryKey: ['my-lost-reports', { page: 1, pageSize: 50, status: 'Active' }],
    queryFn: () => getMyLostReports({ page: 1, pageSize: 50, status: 'Active' }),
    enabled: canRecognise,
  })

  const recognise = useMutation({
    mutationFn: () => recogniseFoundPost(item!.id, reportId),
    onSuccess: () => {
      setDone(true)
      queryClient.invalidateQueries({ queryKey: ['my-suggestions'] })
      toast.success('The finder has been asked to hand it in.')
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : 'Could not reach the server.'),
  })

  const withdraw = useMutation({
    mutationFn: () => withdrawFoundPost(item!.id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['found-feed'] })
      toast.success('Post taken down.')
      onClose()
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : 'Could not reach the server.'),
  })

  return (
    <Sheet
      open={item !== null}
      onOpenChange={(open) => {
        if (!open) {
          onClose()
          setDone(false)
          setReportId('')
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

            <p className="rounded-xl border border-amber-500/30 bg-amber-50 px-3 py-2 text-sm text-amber-900">
              Not at a desk yet. It can be claimed once the finder hands it in.
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

            {item.isMine && item.handInCode && (
              <div className="flex items-center justify-between gap-4 rounded-xl bg-brand-forest px-4 py-3 text-white">
                <div>
                  <p className="text-xs text-white/70">Quote this at the desk when you hand it in</p>
                  <p className="font-mono text-2xl font-semibold tracking-[0.2em] tabular-nums">{displayCode(item.handInCode)}</p>
                </div>
                <HashIcon className="size-6 shrink-0 text-white/60" aria-hidden="true" />
              </div>
            )}

            {item.isMine ? (
              <Button
                variant="outline"
                className="border-neutral-900/15 bg-white/70 text-neutral-800 hover:bg-white"
                onClick={() => withdraw.mutate()}
                disabled={withdraw.isPending}
              >
                {withdraw.isPending ? <Loader2Icon className="animate-spin" aria-hidden="true" /> : <Trash2Icon aria-hidden="true" />}
                Take this post down
              </Button>
            ) : !user ? (
              <Button className="bg-brand-forest text-white hover:bg-brand-forest/90" nativeButton={false} render={<Link to="/login" />}>
                Sign in if this is yours
              </Button>
            ) : canRecognise ? (
              done ? (
                <p className="rounded-xl border border-brand-green/30 bg-brand-green/10 p-4 text-sm text-neutral-700">
                  Done. {item.postedByName.split(' ')[0]} has been asked to hand it in, and it now shows on your report.
                  You will be able to claim it once it reaches a desk.
                </p>
              ) : (
                <div className="flex flex-col gap-3 border-t border-neutral-900/8 pt-5">
                  <div>
                    <p className="text-sm font-medium">Is this yours?</p>
                    <p className="text-xs text-neutral-500">
                      Pick the report it matches. The finder is asked to hand it in; the desk will still check it is
                      yours.
                    </p>
                  </div>
                  <div className="flex flex-col gap-2">
                    <Label htmlFor="recognise-report" className="text-sm text-neutral-900">Your report</Label>
                    <FormSelect
                      id="recognise-report"
                      value={reportId}
                      onValueChange={setReportId}
                      options={(myReports.data?.items ?? []).map((r) => ({ value: r.id, label: `${r.itemTypeName} · ${r.lastSeenLocationName}` }))}
                      placeholder={myReports.isPending ? 'Loading your reports' : 'Choose a report'}
                    />
                  </div>
                  <Button
                    className="bg-brand-forest text-white hover:bg-brand-forest/90"
                    disabled={!reportId || recognise.isPending}
                    onClick={() => recognise.mutate()}
                  >
                    {recognise.isPending && <Loader2Icon className="animate-spin" aria-hidden="true" />}
                    That is mine
                  </Button>
                  {myReports.data && myReports.data.items.length === 0 && (
                    <p className="text-xs text-neutral-500">
                      You have no open report to match it to.{' '}
                      <Link to="/my-reports/new" className="font-medium text-brand-forest underline underline-offset-4">Post one first</Link>.
                    </p>
                  )}
                </div>
              )
            ) : (
              <p className="text-sm text-neutral-500">Staff can pull this post up at the desk by the finder's code.</p>
            )}
          </div>
        )}
      </SheetContent>
    </Sheet>
  )
}

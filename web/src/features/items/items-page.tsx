import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import {
  ChevronLeftIcon,
  ChevronRightIcon,
  EyeOffIcon,
  HashIcon,
  Loader2Icon,
  PackageSearchIcon,
  PlusIcon,
  RotateCwIcon,
  SearchIcon,
} from 'lucide-react'
import { Button } from '@/components/ui/button'
import { DashboardPanel, PanelDivider, PanelSheen } from '@/components/layout/dashboard-panel'
import { panelSurface } from '@/components/layout/panel-surface'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { FormSelect } from '@/features/reports/form-select'
import { formatDateTime } from '@/features/reports/reports-api'
import { toast } from 'sonner'
import { ApiError } from '@/lib/api/client'
import { cn } from '@/lib/utils'
import {
  getItems,
  itemStatusLabel,
  ITEM_STATUS_STYLES,
  resolveDeskCode,
  type DeskCodeMatch,
  type LogItemPrefill,
} from './items-api'

const PAGE_SIZE = 15

const STATUS_OPTIONS = [
  { value: 'Unclaimed', label: 'In storage' },
  { value: 'Posted', label: 'Posted by finders' },
  { value: 'all', label: 'Everything' },
  { value: 'Claimed', label: 'Claimed' },
  { value: 'Returned', label: 'Returned' },
  { value: 'Disposed', label: 'Disposed' },
]

/**
 * Everything handed in at the desk.
 *
 * Defaults to what is still in storage, because that is the working set - a desk needs to
 * know what it is holding, not everything it has ever held. The hidden verification detail
 * is never shown here, only whether one was recorded: the table is read over a counter with
 * the claimant standing on the other side of it.
 */
export function ItemsPage() {
  const [page, setPage] = useState(1)
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('Unclaimed')

  const { data, isPending, isError, error, isFetching, refetch } = useQuery({
    queryKey: ['found-items', { page, search, status }],
    queryFn: () =>
      getItems({
        page,
        pageSize: PAGE_SIZE,
        search,
        status: status === 'all' ? undefined : status,
      }),
    placeholderData: keepPreviousData,
  })

  function handleSearch(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setPage(1)
    setSearch(searchInput)
  }

  const hasFilters = Boolean(search) || status !== 'Unclaimed'

  return (
    <section className="flex flex-col gap-6">
      <DashboardPanel className="flex flex-col gap-6">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">Found items</h1>
            <p className="pt-1 text-sm text-muted-foreground">
              Everything handed in, and where it is being kept.
            </p>
          </div>

          <Button
            className="bg-neutral-900 text-white hover:bg-neutral-900/90 dark:bg-white dark:text-neutral-900 dark:hover:bg-white/90"
            nativeButton={false}
            render={<Link to="/items/new" />}
          >
            <PlusIcon aria-hidden="true" />
            Log an item
          </Button>
        </div>

        <PanelDivider />

        <form onSubmit={handleSearch} className="flex flex-col gap-3 sm:flex-row sm:items-end">
          <div className="flex flex-1 flex-col gap-2">
            <Label htmlFor="item-search">Search</Label>
            <div className="relative">
              <SearchIcon
                className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground"
                aria-hidden="true"
              />
              <Input
                id="item-search"
                value={searchInput}
                onChange={(event) => setSearchInput(event.target.value)}
                placeholder="Description, colour, item type"
                className="pl-9"
              />
            </div>
          </div>

          <div className="flex flex-col gap-2 sm:w-44">
            <Label htmlFor="item-status">Status</Label>
            <FormSelect
              id="item-status"
              value={status}
              onValueChange={(next) => {
                setStatus(next)
                setPage(1)
              }}
              options={STATUS_OPTIONS}
              placeholder="In storage"
            />
          </div>

          <Button type="submit" variant="outline">
            Search
          </Button>
        </form>

        <PanelDivider />

        <PostByCodeBox />
      </DashboardPanel>

      {isPending ? (
        <DashboardPanel className="flex flex-col gap-3">
          {Array.from({ length: 6 }).map((_, index) => (
            <div key={index} className="flex items-center gap-4">
              <Skeleton className="h-4 flex-1" />
              <Skeleton className="h-4 w-40" />
              <Skeleton className="h-5 w-20 rounded-full" />
            </div>
          ))}
        </DashboardPanel>
      ) : isError ? (
        <DashboardPanel
          role="alert"
          className="flex flex-col items-start gap-3 border-destructive/40 from-destructive/8 via-destructive/5 to-transparent dark:from-destructive/15 dark:via-destructive/8"
        >
          <div>
            <p className="font-heading text-base font-medium">Could not load the items</p>
            <p className="pt-1 text-sm text-muted-foreground">
              {error instanceof ApiError ? error.message : 'Check the API is running.'}
            </p>
          </div>
          <Button variant="outline" onClick={() => refetch()}>
            <RotateCwIcon aria-hidden="true" />
            Try again
          </Button>
        </DashboardPanel>
      ) : data.items.length === 0 ? (
        <DashboardPanel className="flex flex-col items-center gap-3 py-16 text-center">
          <span className="flex size-12 items-center justify-center rounded-2xl bg-muted">
            <PackageSearchIcon className="size-5 text-muted-foreground" aria-hidden="true" />
          </span>
          <p className="text-base font-medium">
            {hasFilters ? 'Nothing matches those filters' : 'Nothing in storage'}
          </p>
          <p className="max-w-sm text-sm text-muted-foreground">
            {hasFilters
              ? 'Try a broader search, or switch the status to everything.'
              : 'Items appear here as they are handed in at the desk.'}
          </p>
          <Button className="mt-1" nativeButton={false} render={<Link to="/items/new" />}>
            <PlusIcon aria-hidden="true" />
            Log an item
          </Button>
        </DashboardPanel>
      ) : (
        <>
          <DashboardPanel
            className={cn('p-0 transition-opacity duration-200 sm:p-0', isFetching && 'opacity-60')}
          >
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Item</TableHead>
                    <TableHead>Found</TableHead>
                    <TableHead>Kept at</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead className="text-right">Verification</TableHead>
                  </TableRow>
                </TableHeader>

                <TableBody>
                  {data.items.map((item) => (
                    <TableRow key={item.id}>
                      <TableCell>
                        <Link
                          to={`/items/${item.id}`}
                          className="flex flex-col leading-tight rounded-sm focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none"
                        >
                          <span className="font-medium">
                            {item.itemTypeName}
                            {item.primaryColor && (
                              <span className="pl-2 text-xs font-normal text-muted-foreground">
                                {item.primaryColor}
                              </span>
                            )}
                          </span>
                          <span className="max-w-80 truncate text-xs text-muted-foreground">
                            {item.generalDescription}
                          </span>
                        </Link>
                      </TableCell>

                      <TableCell className="text-sm text-muted-foreground">
                        <div className="flex flex-col leading-tight">
                          <span>{item.foundLocationName}</span>
                          <span className="text-xs">{formatDateTime(item.foundAt)}</span>
                        </div>
                      </TableCell>

                      <TableCell className="text-sm text-muted-foreground">
                        {item.storageLocationName ?? (item.finderName ? `With ${item.finderName}` : '-')}
                      </TableCell>

                      <TableCell>
                        <span
                          className={cn(
                            'inline-flex w-fit items-center gap-1.5 rounded-full border px-2.5 py-0.5 text-xs font-medium whitespace-nowrap',
                            ITEM_STATUS_STYLES[item.status],
                          )}
                        >
                          <span className="size-1.5 rounded-full bg-current" aria-hidden="true" />
                          {itemStatusLabel(item)}
                        </span>
                      </TableCell>

                      {/* Whether a hidden detail exists, never what it is. */}
                      <TableCell className="text-right">
                        {item.hasVerificationDetails ? (
                          <span
                            className="inline-flex items-center gap-1.5 text-xs text-muted-foreground"
                            title="A hidden detail was recorded for this item"
                          >
                            <EyeOffIcon className="size-3.5" aria-hidden="true" />
                            Recorded
                          </span>
                        ) : (
                          <span className="text-xs text-muted-foreground/60">None</span>
                        )}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          </DashboardPanel>

          <nav
            aria-label="Item pages"
            className={cn(panelSurface, 'flex items-center justify-between gap-4 p-3')}
          >
            <PanelSheen />
            <Button
              variant="outline"
              disabled={!data.hasPreviousPage}
              onClick={() => setPage((p) => Math.max(1, p - 1))}
            >
              <ChevronLeftIcon aria-hidden="true" />
              Previous
            </Button>
            <span className="text-xs text-muted-foreground tabular-nums">
              {data.totalCount} item{data.totalCount === 1 ? '' : 's'} · page {data.page} of{' '}
              {data.totalPages}
            </span>
            <Button
              variant="outline"
              disabled={!data.hasNextPage}
              onClick={() => setPage((p) => p + 1)}
            >
              Next
              <ChevronRightIcon aria-hidden="true" />
            </Button>
          </nav>
        </>
      )}
    </section>
  )
}


/**
 * A finder at the counter quotes a code. It may be the code on their own found post, the
 * handover code they got from pressing "I found this" on someone's lost report, or the code
 * printed on that lost report. Whichever it is, this pulls up the right thing, so the desk
 * never has to know which screen a code belongs to before typing it.
 */
function PostByCodeBox() {
  const navigate = useNavigate()
  const [code, setCode] = useState('')
  const [busy, setBusy] = useState(false)
  // A code that means two things (the series are independent) - the desk picks.
  const [choices, setChoices] = useState<DeskCodeMatch[]>([])
  const digits = code.replace(/\s/g, '')
  const ready = digits.length === 6

  function open(match: DeskCodeMatch) {
    if (match.kind === 'found-post') navigate(`/items/${match.id}`)
    else if (match.kind === 'handover') navigate(`/handovers?code=${digits}`)
    else {
      const prefill: LogItemPrefill = {
        code: digits,
        categoryId: match.categoryId,
        itemTypeId: match.itemTypeId,
        primaryColor: match.primaryColor,
      }
      navigate('/items/new', { state: { prefill } })
    }
  }

  async function lookUp(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!ready) return
    setBusy(true)
    setChoices([])
    try {
      const matches = await resolveDeskCode(digits)
      if (matches.length === 1) open(matches[0])
      else setChoices(matches)
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : 'Could not reach the server.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="flex flex-col gap-3">
      <form onSubmit={lookUp} className="flex flex-col gap-3 sm:flex-row sm:items-end">
        <div className="flex flex-1 flex-col gap-2">
          <Label htmlFor="post-code">A finder is handing something in</Label>
          <Input
            id="post-code"
            value={code}
            onChange={(event) => setCode(event.target.value.replace(/[^\d\s]/g, '').slice(0, 7))}
            inputMode="numeric"
            placeholder="Any code they quote"
            className="font-mono text-lg tracking-[0.2em]"
            autoComplete="off"
            aria-describedby="post-code-help"
          />
        </div>
        <Button type="submit" variant="outline" disabled={!ready || busy}>
          {busy ? <Loader2Icon className="animate-spin" aria-hidden="true" /> : <HashIcon aria-hidden="true" />}
          Pull it up
        </Button>
      </form>
      <p id="post-code-help" className="text-xs text-muted-foreground">
        The code on their found post, the handover code from someone&apos;s lost report, or the
        code printed on that report - any of them works.
      </p>
      {choices.length > 1 && (
        <div role="group" aria-label="That code matches more than one thing" className="flex flex-col gap-2">
          <p className="text-sm">That code matches more than one thing - which is it?</p>
          {choices.map((match) => (
            <button
              key={`${match.kind}-${match.id}`}
              type="button"
              onClick={() => open(match)}
              className="flex flex-col items-start rounded-xl border border-foreground/10 px-4 py-3 text-left transition-colors hover:bg-foreground/[0.03] focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
            >
              <span className="font-medium">{match.title}</span>
              <span className="text-sm text-muted-foreground">{match.detail}</span>
            </button>
          ))}
        </div>
      )}
    </div>
  )
}

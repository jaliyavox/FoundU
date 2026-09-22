import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  ArrowLeftIcon,
  ClockIcon,
  EyeOffIcon,
  LinkIcon,
  Loader2Icon,
  MapPinIcon,
  PackageCheckIcon,
  PackageIcon,
  RotateCwIcon,
  UserIcon,
} from 'lucide-react'
import { Button } from '@/components/ui/button'
import { DashboardPanel, PanelDivider } from '@/components/layout/dashboard-panel'
import { Skeleton } from '@/components/ui/skeleton'
import { getSuggestionsForItem } from '@/features/claims/claims-api'
import { formatDateTime, getStorageLocations } from '@/features/reports/reports-api'
import { FormSelect } from '@/features/reports/form-select'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { toast } from 'sonner'
import { ApiError } from '@/lib/api/client'
import { cn } from '@/lib/utils'
import { type FoundReportDetail, confirmFoundPost, getItem, ITEM_STATUS_LABELS, ITEM_STATUS_STYLES } from './items-api'
import { LinkReportDialog } from './link-report-dialog'

/**
 * One item, as the desk sees it - including the hidden detail that verification rests on.
 *
 * This screen is Staff/Admin only and it is the one place the private evidence appears, so
 * it is set apart rather than mixed into the description: whoever is reading it over a
 * counter needs to know at a glance what must not be said out loud.
 */
export function ItemDetailPage() {
  const { id = '' } = useParams()
  const [linking, setLinking] = useState(false)

  const { data: item, isPending, isError, error, refetch } = useQuery({
    queryKey: ['found-item', id],
    queryFn: () => getItem(id),
  })

  const { data: suggestions } = useQuery({
    queryKey: ['item-suggestions', id],
    queryFn: () => getSuggestionsForItem(id),
    enabled: Boolean(item),
  })

  if (isPending) {
    return (
      <section className="mx-auto flex w-full max-w-3xl flex-col gap-6">
        <Skeleton className="h-7 w-64" />
        <DashboardPanel className="flex flex-col gap-4">
          <Skeleton className="h-4 w-40" />
          <Skeleton className="h-3 w-full" />
          <Skeleton className="h-3 w-2/3" />
        </DashboardPanel>
      </section>
    )
  }

  if (isError) {
    return (
      <DashboardPanel
        role="alert"
        className="mx-auto flex w-full max-w-3xl flex-col items-start gap-3 border-destructive/40 from-destructive/8 via-destructive/5 to-transparent dark:from-destructive/15 dark:via-destructive/8"
      >
        <div>
          <p className="font-heading text-base font-medium">Could not load this item</p>
          <p className="pt-1 text-sm text-muted-foreground">
            {error instanceof ApiError ? error.message : 'Check the API is running.'}
          </p>
        </div>
        <Button variant="outline" onClick={() => refetch()}>
          <RotateCwIcon aria-hidden="true" />
          Try again
        </Button>
      </DashboardPanel>
    )
  }

  return (
    <section className="mx-auto flex w-full max-w-3xl flex-col gap-6">
      <Button
        variant="ghost"
        size="sm"
        className="self-start text-muted-foreground"
        nativeButton={false}
        render={<Link to="/items" />}
      >
        <ArrowLeftIcon aria-hidden="true" />
        Back to items
      </Button>

      <DashboardPanel className="flex flex-col gap-4">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">{item.itemTypeName}</h1>
            <p className="pt-1 text-sm text-muted-foreground">
              {item.categoryName}
              {item.primaryColor && ` · ${item.primaryColor}`}
              {item.secondaryColor && ` and ${item.secondaryColor}`}
            </p>
          </div>

          <span
            className={cn(
              'inline-flex w-fit items-center gap-1.5 rounded-full border px-2.5 py-0.5 text-xs font-medium whitespace-nowrap',
              ITEM_STATUS_STYLES[item.status],
            )}
          >
            <span className="size-1.5 rounded-full bg-current" aria-hidden="true" />
            {ITEM_STATUS_LABELS[item.status]}
          </span>
        </div>

        <p className="text-sm text-pretty text-muted-foreground">{item.generalDescription}</p>

        <PanelDivider />

        <dl className="grid gap-3 sm:grid-cols-2">
          <Fact icon={MapPinIcon} label="Found at" value={item.foundLocationName} />
          <Fact icon={ClockIcon} label="Found" value={formatDateTime(item.foundAt)} />
          <Fact icon={PackageIcon} label="Kept at" value={item.storageLocationName ?? 'Not at a desk yet'} />
          <Fact icon={UserIcon} label={item.finderName ? 'Found by' : 'Logged by'} value={item.finderName ?? item.staffName ?? '-'} />
        </dl>
      </DashboardPanel>

      {item.status === 'Posted' && <ConfirmPostPanel item={item} />}

      {/* Set apart deliberately: this is the one thing on the screen that must not be read
          out to whoever is standing at the counter. */}
      <DashboardPanel className="flex flex-col gap-3 border-amber-500/30 from-amber-500/8 via-amber-500/4 to-transparent dark:from-amber-500/12">
        <div className="flex items-center gap-2">
          <EyeOffIcon className="size-4 text-amber-700 dark:text-amber-300" aria-hidden="true" />
          <h2 className="font-heading text-base font-medium">Hidden verification detail</h2>
        </div>

        {item.privateVerificationDetails ? (
          <>
            <p className="text-sm text-pretty">{item.privateVerificationDetails}</p>
            <p className="text-xs text-muted-foreground">
              Staff only. Never shown to a claimant and never sent in any student-facing
              response - the claim's questions are written from it, and answering them from
              memory is what proves ownership.
            </p>
          </>
        ) : (
          <p className="text-sm text-muted-foreground">
            None recorded. Nothing about this item can be verified from memory, so a claim on
            it needs a supervisor rather than an automatic check.
          </p>
        )}
      </DashboardPanel>

      <DashboardPanel className="flex flex-col gap-4">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <h2 className="font-heading text-base font-medium">Suggested to</h2>
            <p className="pt-1 text-sm text-muted-foreground">
              Students who have been told this might be theirs.
            </p>
          </div>

          {(item.status === 'Unclaimed' || item.status === 'Posted') && (
            <Button variant="outline" onClick={() => setLinking(true)}>
              <LinkIcon aria-hidden="true" />
              Suggest to a report
            </Button>
          )}
        </div>

        {(suggestions?.length ?? 0) === 0 ? (
          <p className="text-sm text-muted-foreground">
            Nobody yet. Find the report it matches and suggest it.
          </p>
        ) : (
          <ul className="flex flex-col gap-2">
            {suggestions!.map((suggestion) => (
              <li
                key={suggestion.id}
                className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-foreground/8 bg-background/50 p-3"
              >
                <div className="min-w-0">
                  <p className="truncate text-sm">{suggestion.lostReportDescription}</p>
                  <p className="text-xs text-muted-foreground">
                    {suggestion.status === 'Confirmed'
                      ? 'They opened a claim'
                      : suggestion.status === 'Dismissed'
                        ? 'They said it is not theirs'
                        : 'Waiting on them'}
                    {' · '}
                    {formatDateTime(suggestion.createdAt)}
                  </p>
                </div>

                {suggestion.claimId && (
                  <Button
                    size="sm"
                    variant="ghost"
                    nativeButton={false}
                    render={<Link to={`/claims/${suggestion.claimId}`} />}
                  >
                    Open the claim
                  </Button>
                )}
              </li>
            ))}
          </ul>
        )}
      </DashboardPanel>

      <LinkReportDialog item={item} open={linking} onOpenChange={setLinking} />
    </section>
  )
}

function Fact({
  icon: Icon,
  label,
  value,
}: {
  icon: typeof MapPinIcon
  label: string
  value: string
}) {
  return (
    <div className="flex items-start gap-3">
      <Icon className="mt-0.5 size-4 shrink-0 text-brand-green" aria-hidden="true" />
      <div>
        <dt className="text-xs text-muted-foreground">{label}</dt>
        <dd className="text-sm">{value}</dd>
      </div>
    </div>
  )
}


/**
 * The desk turning a finder's post into a real record. This is the moment the hidden detail
 * gets written - the one thing the finder was told not to put on the feed - so the panel
 * asks for it plainly and explains why.
 */
function ConfirmPostPanel({ item }: { item: FoundReportDetail }) {
  const queryClient = useQueryClient()
  const storage = useQuery({ queryKey: ['storage-locations'], queryFn: getStorageLocations })
  const [storageLocationId, setStorageLocationId] = useState('')
  const [privateDetail, setPrivateDetail] = useState('')
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({})

  const confirm = useMutation({
    mutationFn: () =>
      confirmFoundPost(item.id, { storageLocationId, privateVerificationDetails: privateDetail.trim() || undefined }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['found-item', item.id] })
      queryClient.invalidateQueries({ queryKey: ['found-items'] })
      toast.success('Confirmed. It is in storage and can be claimed now.')
    },
    onError: (error) => {
      if (error instanceof ApiError) {
        setFieldErrors(error.fieldErrors)
        if (Object.keys(error.fieldErrors).length === 0) toast.error(error.message)
      } else {
        toast.error('Could not reach the server.')
      }
    },
  })

  return (
    <DashboardPanel className="flex flex-col gap-4 border-amber-500/30 from-amber-500/8 via-amber-500/4 to-transparent dark:from-amber-500/12">
      <div>
        <h2 className="font-heading text-base font-medium">Confirm it at the desk</h2>
        <p className="pt-1 text-sm text-muted-foreground">
          {item.finderName ?? 'A student'} posted this and has now handed it in. Say where it is kept and
          record one detail the finder did not publish. From then on it can be claimed.
        </p>
        {item.handInCode && (
          <p className="pt-2 text-xs text-muted-foreground">
            Their code: <span className="font-mono font-medium tracking-wider text-foreground">{item.handInCode.slice(0, 3)} {item.handInCode.slice(3)}</span>
          </p>
        )}
      </div>

      <div className="flex flex-col gap-2">
        <Label htmlFor="confirm-storage">Kept at</Label>
        <FormSelect
          id="confirm-storage"
          value={storageLocationId}
          onValueChange={setStorageLocationId}
          options={(storage.data ?? []).map((s) => ({ value: s.id, label: s.name }))}
          placeholder={storage.isPending ? 'Loading' : 'Where it is stored'}
          invalid={Boolean(fieldErrors.StorageLocationId)}
        />
      </div>

      <div className="flex flex-col gap-2">
        <Label htmlFor="confirm-private">Hidden verification detail</Label>
        <Textarea
          id="confirm-private"
          rows={2}
          value={privateDetail}
          onChange={(event) => setPrivateDetail(event.target.value)}
          placeholder="Name written inside the collar; a bus ticket in the left pocket."
        />
        <p className="text-xs text-pretty text-muted-foreground">
          Never shown to a claimant. Leave it blank if there is genuinely nothing distinctive - do not invent one.
        </p>
      </div>

      <Button
        className="self-start bg-brand-forest text-white hover:bg-brand-forest/90"
        disabled={!storageLocationId || confirm.isPending}
        onClick={() => confirm.mutate()}
      >
        {confirm.isPending ? <Loader2Icon className="animate-spin" aria-hidden="true" /> : <PackageCheckIcon aria-hidden="true" />}
        Confirm and shelve it
      </Button>
    </DashboardPanel>
  )
}

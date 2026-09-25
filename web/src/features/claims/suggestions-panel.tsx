import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ClockIcon, Loader2Icon, MapPinIcon, PackageSearchIcon, XIcon } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { DashboardPanel, PanelDivider } from '@/components/layout/dashboard-panel'
import { ApiError } from '@/lib/api/client'
import { formatDateTime } from '@/features/reports/reports-api'
import { createClaim, dismissSuggestion, getMySuggestions, type MatchSuggestion } from './claims-api'

/**
 * "The desk thinks they may have your item."
 *
 * There is no browsable list of found items - that is how someone shops for something to
 * claim - so this panel is the only way a student learns that one specific item might be
 * theirs. Staff make the link by hand today; the Matching Agent writes the same rows later,
 * and this panel will not need to change when it does.
 */
export function SuggestionsPanel() {
  const { data } = useQuery({
    queryKey: ['my-suggestions'],
    queryFn: () => getMySuggestions(),
  })

  const open = data?.items ?? []

  // Nothing to say is better than an empty box saying nothing.
  if (open.length === 0) return null

  return (
    <DashboardPanel className="flex flex-col gap-5">
      <div>
        <h2 className="font-heading text-lg font-medium">
          {open.length === 1 ? 'An item that might be yours' : `${open.length} items that might be yours`}
        </h2>
        <p className="pt-1 text-sm text-muted-foreground">
          Staff matched these against your reports. Say whether each one is yours - it is not
          yours until you can describe something about it that was never published.
        </p>
      </div>

      <PanelDivider />

      <ul className="flex flex-col gap-4">
        {open.map((suggestion) => (
          <li key={suggestion.id}>
            <SuggestionRow suggestion={suggestion} />
          </li>
        ))}
      </ul>
    </DashboardPanel>
  )
}

function SuggestionRow({ suggestion }: { suggestion: MatchSuggestion }) {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [confirmingDismiss, setConfirmingDismiss] = useState(false)

  const item = suggestion.foundItem

  const claim = useMutation({
    mutationFn: () => createClaim(suggestion.lostReportId, item.id),
    onSuccess: (created) => {
      queryClient.invalidateQueries({ queryKey: ['my-suggestions'] })
      queryClient.invalidateQueries({ queryKey: ['my-lost-reports'] })
      queryClient.invalidateQueries({ queryKey: ['my-claims'] })
      navigate(`/claims/${created.id}`)
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : 'Could not reach the server.'),
  })

  const dismiss = useMutation({
    mutationFn: () => dismissSuggestion(suggestion.id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['my-suggestions'] })
      toast.success('Taken off your list.')
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : 'Could not reach the server.'),
  })

  const isBusy = claim.isPending || dismiss.isPending

  return (
    <div className="flex flex-col gap-4 rounded-xl border border-foreground/8 bg-background/50 p-4 sm:p-5">
      <div className="flex items-start gap-3">
        <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-foreground text-background">
          <PackageSearchIcon className="size-5" aria-hidden="true" />
        </span>

        <div className="min-w-0 flex-1">
          <h3 className="font-medium">{item.itemTypeName}</h3>
          <p className="text-sm text-pretty text-muted-foreground">{item.generalDescription}</p>

          <div className="flex flex-wrap gap-x-4 gap-y-1 pt-2 text-xs text-muted-foreground">
            <span className="flex items-center gap-1.5">
              <MapPinIcon className="size-3.5 text-brand-green" aria-hidden="true" />
              Handed in at {item.foundLocationName}
            </span>
            <span className="flex items-center gap-1.5">
              <ClockIcon className="size-3.5 text-brand-green" aria-hidden="true" />
              {formatDateTime(item.foundAt)}
            </span>
          </div>
        </div>
      </div>

      {suggestion.note && (
        <p className="border-l-2 border-brand-green/40 pl-3 text-sm text-pretty text-muted-foreground">
          {suggestion.note}
        </p>
      )}

      {suggestion.claimId ? (
        <Button
          variant="outline"
          className="self-start"
          onClick={() => navigate(`/claims/${suggestion.claimId}`)}
        >
          View your claim
        </Button>
      ) : confirmingDismiss ? (
        <div className="flex flex-wrap items-center gap-3">
          <p className="text-sm text-muted-foreground">
            Take it off your list? Staff keep the item either way.
          </p>
          <Button size="sm" variant="outline" onClick={() => dismiss.mutate()} disabled={isBusy}>
            {dismiss.isPending && <Loader2Icon className="animate-spin" aria-hidden="true" />}
            Yes, not mine
          </Button>
          <Button size="sm" variant="ghost" onClick={() => setConfirmingDismiss(false)} disabled={isBusy}>
            Keep it
          </Button>
        </div>
      ) : (
        <div className="flex flex-wrap gap-2">
          <Button
            className="bg-brand-forest text-white hover:bg-brand-forest/90"
            onClick={() => claim.mutate()}
            disabled={isBusy}
          >
            {claim.isPending && <Loader2Icon className="animate-spin" aria-hidden="true" />}
            This is mine
          </Button>
          <Button variant="ghost" onClick={() => setConfirmingDismiss(true)} disabled={isBusy}>
            <XIcon aria-hidden="true" />
            Not mine
          </Button>
        </div>
      )}
    </div>
  )
}

import { useState } from 'react'
import { Label } from '@/components/ui/label'
import { FormSelect } from '@/features/reports/form-select'
import { Link, useNavigate } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Loader2Icon } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { claimWithoutReport, createClaim, dismissSuggestion, getMySuggestionsForItem, type MatchSuggestion } from '@/features/claims/claims-api'
import { Textarea } from '@/components/ui/textarea'
import { getMyLostReports, type LostReportListItem } from '@/features/reports/reports-api'
import { ApiError } from '@/lib/api/client'
import { recogniseFoundPost, type FoundPostItem } from './feed-api'

const errorText = (error: unknown) => (error instanceof ApiError ? error.message : 'Could not reach the server.')

/**
 * What a student who thinks an item is theirs can do, from the item itself.
 *
 *  - A claim already open on it        View claim
 *  - Automatic matches, if any         "Is this your item?" Yes (a claim bound to that match) / No
 *  - Always, the manual way            pick the lost report it belongs to, "That is mine" - which
 *                                      records the match (recognise) and, once the item is at a
 *                                      desk, opens the claim in the same step
 *  - No report at all                  claim it anyway, in their own words (item at a desk) -
 *                                      staff ask their questions on the claim as usual; or go to
 *                                      the desk, where staff can verify them in person
 *
 * Automatic matching only assists: the manual way is offered whether or not anything matched,
 * as it was before matches existed. Without it, an owner nobody had matched to the item was sent
 * to "Open your matches", where the item never appeared.
 */
export function FoundPostOwnerActions({ item }: { item: FoundPostItem }) {
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const [hidden, setHidden] = useState<string[]>([])
  const [reportId, setReportId] = useState('')
  const [ownWords, setOwnWords] = useState('')
  const atDesk = item.status === 'Unclaimed'

  const matches = useQuery({
    queryKey: ['my-suggestions', 'item', item.id],
    queryFn: () => getMySuggestionsForItem(item.id),
    refetchInterval: 30_000,
  })
  const own = (matches.data?.items ?? []).filter(
    (m) => m.foundItem.id === item.id && m.status !== 'Dismissed' && !hidden.includes(m.id),
  )
  const claimed = own.find((m) => m.claimId)
  const eligible = own.filter((m) => m.status === 'Suggested' && !m.claimId)

  const reports = useQuery({
    queryKey: ['my-lost-reports', { page: 1, pageSize: 50, status: 'Active' }],
    queryFn: () => getMyLostReports({ page: 1, pageSize: 50, status: 'Active' }),
    enabled: matches.isSuccess && !claimed,
  })

  function refresh() {
    for (const key of ['my-suggestions', 'my-lost-reports', 'my-claims']) queryClient.invalidateQueries({ queryKey: [key] })
  }

  const claim = useMutation({
    mutationFn: (match: MatchSuggestion) => createClaim(match.lostReportId, item.id, match.id),
    onSuccess: (created) => {
      refresh()
      toast.success('Claim submitted - the desk will ask you a question only the owner could answer.')
      navigate(`/claims/${created.id}`)
    },
    onError: (error) => toast.error(errorText(error)),
  })

  const notMine = useMutation({
    mutationFn: (match: MatchSuggestion) => dismissSuggestion(match.id, 'Not mine (from the found item).'),
    onSuccess: (_, match) => {
      setHidden((ids) => [...ids, match.id])
      refresh()
      toast.success('Thanks - we will not suggest it for that report again.')
    },
    onError: (error) => toast.error(errorText(error)),
  })

  // "This is mine" on one of their reports: record the match, and if the desk has the item,
  // open the claim on that match straight away.
  const link = useMutation({
    mutationFn: async (report: Pick<LostReportListItem, 'id'>) => {
      await recogniseFoundPost(item.id, report.id)
      if (!atDesk) return null
      const fresh = await getMySuggestionsForItem(item.id)
      const match = fresh.items.find((m) => m.lostReportId === report.id && m.foundItem.id === item.id)
      return match ? createClaim(report.id, item.id, match.id) : null
    },
    onSuccess: (created) => {
      refresh()
      if (created) {
        toast.success('Claim submitted - the desk will ask you a question only the owner could answer.')
        navigate(`/claims/${created.id}`)
      } else {
        toast.success(`Linked to your report. ${item.postedByName.split(' ')[0]} has been asked to hand it in - you can claim it once it reaches the desk.`)
      }
    },
    onError: (error) => toast.error(errorText(error)),
  })

  const withoutReport = useMutation({
    mutationFn: () => claimWithoutReport(item.id, ownWords.trim()),
    onSuccess: (created) => {
      refresh()
      toast.success('Claim submitted - the desk will ask you a question only the owner could answer.')
      navigate(`/claims/${created.id}`)
    },
    onError: (error) => toast.error(errorText(error)),
  })

  const busy = claim.isPending || notMine.isPending || link.isPending || withoutReport.isPending

  if (matches.isPending) {
    return <p className="text-sm text-neutral-500">Checking your reports…</p>
  }

  if (claimed?.claimId) {
    return (
      <div className="flex flex-col gap-2">
        <p className="text-sm">You have already claimed this item.</p>
        <Button nativeButton={false} variant="outline" className="self-start" render={<Link to={`/claims/${claimed.claimId}`} />}>
          View claim
        </Button>
      </div>
    )
  }

  const openReports = reports.data?.items ?? []

  return (
    <div className="flex flex-col gap-4">
      {eligible.length > 0 && !atDesk && (
        <p className="text-xs text-neutral-500">You can claim it once the finder hands it in at a security desk.</p>
      )}
      {eligible.map((match) => (
        <div key={match.id} className="flex flex-col gap-2 rounded-xl border border-neutral-900/8 bg-white/70 p-4">
          <p className="text-xs text-neutral-500">Your lost report: {match.lostReportDescription}</p>
          <p className="text-sm font-medium">Is this your item?</p>
          <div className="flex flex-wrap gap-2">
            <Button
              className="bg-brand-forest text-white hover:bg-brand-forest/90"
              disabled={busy || !atDesk}
              onClick={() => claim.mutate(match)}
            >
              {claim.isPending && claim.variables?.id === match.id && <Loader2Icon className="animate-spin" aria-hidden="true" />}
              Yes, this is mine
            </Button>
            <Button variant="outline" disabled={busy} onClick={() => notMine.mutate(match)}>
              No, this is not mine
            </Button>
          </div>
        </div>
      ))}

      {/* The manual way, always offered. */}
      <div className="flex flex-col gap-3 rounded-xl border border-neutral-900/8 bg-white/70 p-4">
        <p className="text-sm font-medium">{eligible.length > 0 ? 'Or link it to another of your reports' : 'Sure it is yours? Link it to your report'}</p>
        <p className="text-xs text-neutral-500">
          {atDesk
            ? 'This opens a claim. The desk asks you something only the owner would know before handing it over.'
            : `${item.postedByName.split(' ')[0]} is asked to hand it in to security; you can claim it once it is there.`}
        </p>
        {reports.isPending ? (
          <p className="text-sm text-neutral-500">Loading your reports…</p>
        ) : openReports.length === 0 ? (
          <p className="text-sm text-neutral-600">
            You have no open lost report to link it to.{' '}
            <Link to="/my-reports/new" className="font-medium text-brand-forest underline underline-offset-4">
              Report it lost
            </Link>
            {atDesk ? ', or claim it without one below.' : ' first, then come back to this item.'}
          </p>
        ) : (
          <>
            <div className="flex flex-col gap-2">
              <Label htmlFor="recognise-report" className="text-sm text-neutral-900">Your report</Label>
              <FormSelect
                id="recognise-report"
                value={reportId}
                onValueChange={setReportId}
                options={openReports.map((r) => ({ value: r.id, label: `${r.itemTypeName} · ${r.lastSeenLocationName}` }))}
                placeholder="Choose a report"
              />
            </div>
            <Button
              className="self-start bg-brand-forest text-white hover:bg-brand-forest/90"
              disabled={!reportId || busy}
              onClick={() => link.mutate({ id: reportId })}
            >
              {link.isPending && <Loader2Icon className="animate-spin" aria-hidden="true" />}
              That is mine
            </Button>
          </>
        )}
      </div>

      {/* No report needed: a claim in their own words, answered on the dashboard as usual. */}
      {atDesk && (
        <details
          className="rounded-xl border border-neutral-900/8 bg-white/70 p-4"
          open={!reports.isPending && openReports.length === 0}
        >
          <summary className="cursor-pointer text-sm font-medium">Never reported it lost? Claim it anyway</summary>
          <div className="flex flex-col gap-3 pt-3">
            <p className="text-xs text-neutral-500">
              Describe your item in your own words. The desk sends you a question only the owner could
              answer - you reply here, on your claims. You can also go to the desk and prove it in person.
            </p>
            <Label htmlFor="own-words" className="text-sm text-neutral-900">What is it like?</Label>
            <Textarea
              id="own-words"
              rows={3}
              maxLength={1000}
              value={ownWords}
              onChange={(event) => setOwnWords(event.target.value)}
              placeholder="Yellow leather wallet, a bit worn at the corners. My student card is inside."
            />
            <Button
              className="self-start bg-brand-forest text-white hover:bg-brand-forest/90"
              disabled={ownWords.trim().length < 10 || busy}
              onClick={() => withoutReport.mutate()}
            >
              {withoutReport.isPending && <Loader2Icon className="animate-spin" aria-hidden="true" />}
              Claim this item
            </Button>
          </div>
        </details>
      )}
    </div>
  )
}

import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  SparklesIcon,
  FlagIcon,
  MapPinIcon,
  CalendarIcon,
  TagIcon,
  CheckCircle2Icon,
  AlertTriangleIcon,
  Loader2Icon,
} from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Textarea } from '@/components/ui/textarea'
import { Label } from '@/components/ui/label'
import {
  getLostReport,
  getPossibleMatches,
  flagLostReport,
  formatDateTime,
} from './reports-api'
import { ItemIllustration } from '@/features/feed/item-illustration'
import { ApiError } from '@/lib/api/client'

interface LostReportDetailDialogProps {
  reportId: string | null
  open: boolean
  onClose: () => void
  onEdit?: () => void
}

export function LostReportDetailDialog({
  reportId,
  open,
  onClose,
  onEdit,
}: LostReportDetailDialogProps) {
  const queryClient = useQueryClient()
  const [flagReason, setFlagReason] = useState('')
  const [showFlagForm, setShowFlagForm] = useState(false)

  const { data: detail, isPending: isReportPending } = useQuery({
    queryKey: ['lost-report', reportId],
    queryFn: () => (reportId ? getLostReport(reportId) : null),
    enabled: !!reportId && open,
  })

  const { data: possibleMatches = [] } = useQuery({
    queryKey: ['possible-matches', reportId],
    queryFn: () => (reportId ? getPossibleMatches(reportId) : []),
    enabled: !!reportId && open,
  })

  const flagMutation = useMutation({
    mutationFn: async () => {
      if (!reportId || !flagReason.trim()) return
      return flagLostReport(reportId, flagReason.trim(), 'duplicate_or_inappropriate')
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['my-lost-reports'] })
      queryClient.invalidateQueries({ queryKey: ['lost-report', reportId] })
      toast.success('Report flagged for staff review.')
      setShowFlagForm(false)
      setFlagReason('')
    },
    onError: (err) => {
      toast.error(err instanceof ApiError ? err.message : 'Could not flag report.')
    },
  })

  // Parse structured AI attributes if present
  let parsedAiAttrs: Record<string, unknown> | null = null
  if (detail?.parsedAttributesJson) {
    try {
      parsedAiAttrs = JSON.parse(detail.parsedAttributesJson)
    } catch {
      parsedAiAttrs = null
    }
  }

  return (
    <Dialog open={open} onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="max-h-[85vh] max-w-2xl overflow-y-auto">
        <DialogHeader>
          <div className="flex items-center justify-between gap-4">
            <DialogTitle className="flex items-center gap-2 text-xl">
              <TagIcon className="size-5 text-primary" />
              {detail ? `${detail.itemTypeName} Report` : 'Lost Report Details'}
            </DialogTitle>
            {detail?.status === 'Active' && onEdit && (
              <Button size="sm" variant="outline" onClick={onEdit}>
                Edit Report
              </Button>
            )}
          </div>
          <DialogDescription>
            Detailed view, extracted AI attributes, and possible matching found items.
          </DialogDescription>
        </DialogHeader>

        {isReportPending || !detail ? (
          <div className="flex h-40 items-center justify-center">
            <Loader2Icon className="size-6 animate-spin text-muted-foreground" />
          </div>
        ) : (
          <div className="space-y-6 py-2">
            {/* Basic overview card */}
            <div className="relative overflow-hidden rounded-xl border bg-muted/40 p-4">
              <span
                aria-hidden="true"
                className="pointer-events-none absolute -top-4 -right-4 size-32 text-foreground/5"
              >
                <ItemIllustration itemType={detail.itemTypeName} category={detail.categoryName} />
              </span>

              <div className="flex flex-wrap items-center justify-between gap-2 pb-3">
                <Badge variant={detail.status === 'Active' ? 'default' : 'secondary'}>
                  {detail.status}
                </Badge>
                <span className="text-xs text-muted-foreground">
                  Reported {formatDateTime(detail.createdAt)}
                </span>
              </div>

              <h3 className="text-base font-semibold text-foreground">{detail.description}</h3>

              <div className="mt-3 grid grid-cols-1 gap-2 text-sm text-muted-foreground sm:grid-cols-2">
                <div className="flex items-center gap-2">
                  <TagIcon className="size-4 shrink-0" />
                  <span>Category: </span>
                  <strong className="text-foreground">{detail.categoryName}</strong>
                </div>
                <div className="flex items-center gap-2">
                  <MapPinIcon className="size-4 shrink-0" />
                  <span>Location: </span>
                  <strong className="text-foreground">{detail.lastSeenLocationName}</strong>
                </div>
                <div className="flex items-center gap-2 sm:col-span-2">
                  <CalendarIcon className="size-4 shrink-0" />
                  <span>Lost between: </span>
                  <strong className="text-foreground">
                    {formatDateTime(detail.estimatedLostFromAt)} - {formatDateTime(detail.estimatedLostToAt)}
                  </strong>
                </div>
                {detail.primaryColor && (
                  <div className="flex items-center gap-2">
                    <span>Primary Color: </span>
                    <strong className="text-foreground">{detail.primaryColor}</strong>
                  </div>
                )}
                {detail.secondaryColor && (
                  <div className="flex items-center gap-2">
                    <span>Secondary Color: </span>
                    <strong className="text-foreground">{detail.secondaryColor}</strong>
                  </div>
                )}
              </div>

              {detail.photos && detail.photos.length > 0 && (
                <div className="mt-4">
                  <p className="text-xs font-medium text-muted-foreground">Attached Photos:</p>
                  <div className="mt-2 flex gap-2 overflow-x-auto">
                    {detail.photos.map((p) => (
                      <img
                        key={p.id}
                        src={p.url}
                        alt="Lost item photo"
                        className="size-20 rounded-lg object-cover border"
                      />
                    ))}
                  </div>
                </div>
              )}
            </div>

            {/* Extracted AI attributes section */}
            <div className="rounded-xl border border-primary/20 bg-primary/5 p-4">
              <div className="flex items-center gap-2 font-medium text-primary">
                <SparklesIcon className="size-4" />
                AI Extracted Attributes
              </div>
              {parsedAiAttrs && Object.keys(parsedAiAttrs).length > 0 ? (
                <div className="mt-3 flex flex-wrap gap-2">
                  {Object.entries(parsedAiAttrs).map(([key, val]) => (
                    <Badge key={key} variant="outline" className="bg-background text-xs">
                      <span className="text-muted-foreground mr-1">{key}:</span>
                      <span>{String(val)}</span>
                    </Badge>
                  ))}
                </div>
              ) : (
                <p className="mt-2 text-xs text-muted-foreground">
                  The AI parser will process this description to extract attributes for automated item matching.
                </p>
              )}
            </div>

            {/* Possible matches section */}
            <div>
              <h4 className="flex items-center gap-2 text-sm font-semibold text-foreground">
                <CheckCircle2Icon className="size-4 text-emerald-600" />
                Possible Matches ({possibleMatches.length})
              </h4>

              {possibleMatches.length === 0 ? (
                <p className="mt-2 text-xs text-muted-foreground">
                  No match suggestions generated for this item yet. You will be notified as soon as a potential match is handed in.
                </p>
              ) : (
                <ul className="mt-3 space-y-2">
                  {possibleMatches.map((match) => (
                    <li
                      key={match.id}
                      className="flex flex-col gap-2 rounded-lg border bg-background p-3 text-sm"
                    >
                      <div className="flex items-center justify-between">
                        <span className="font-medium text-foreground">
                          {match.foundItem.itemTypeName} at {match.foundItem.foundLocationName}
                        </span>
                        <Badge variant="outline" className="text-xs">
                          {match.status}
                        </Badge>
                      </div>
                      <p className="text-xs text-muted-foreground">
                        {match.foundItem.generalDescription}
                      </p>
                      <div className="flex items-center justify-between text-xs text-muted-foreground">
                        <span>Found on {formatDateTime(match.foundItem.foundAt)}</span>
                        {match.matchScore && (
                          <span className="font-medium text-primary">
                            Match Score: {Math.round(match.matchScore * 100)}%
                          </span>
                        )}
                      </div>
                    </li>
                  ))}
                </ul>
              )}
            </div>

            {/* Flag duplicate or inappropriate section */}
            <div className="border-t pt-4">
              {!showFlagForm ? (
                <Button
                  variant="ghost"
                  size="sm"
                  className="text-xs text-muted-foreground hover:text-destructive"
                  onClick={() => setShowFlagForm(true)}
                >
                  <FlagIcon className="mr-1.5 size-3.5" />
                  Mark duplicate or inappropriate report
                </Button>
              ) : (
                <div className="space-y-3 rounded-lg border border-destructive/30 bg-destructive/5 p-3">
                  <div className="flex items-center gap-2 text-xs font-medium text-destructive">
                    <AlertTriangleIcon className="size-4" />
                    Flag Report for Staff Review
                  </div>
                  <Label htmlFor="flagReason" className="text-xs">
                    Reason for marking this report
                  </Label>
                  <Textarea
                    id="flagReason"
                    value={flagReason}
                    onChange={(e) => setFlagReason(e.target.value)}
                    placeholder="Specify why this report is inappropriate, duplicate, or spam..."
                    rows={2}
                    className="text-xs"
                  />
                  <div className="flex justify-end gap-2">
                    <Button
                      size="sm"
                      variant="outline"
                      onClick={() => setShowFlagForm(false)}
                      disabled={flagMutation.isPending}
                    >
                      Cancel
                    </Button>
                    <Button
                      size="sm"
                      variant="destructive"
                      onClick={() => flagMutation.mutate()}
                      disabled={flagMutation.isPending || !flagReason.trim()}
                    >
                      {flagMutation.isPending && <Loader2Icon className="mr-1.5 size-3 animate-spin" />}
                      Submit Flag
                    </Button>
                  </div>
                </div>
              )}
            </div>
          </div>
        )}
      </DialogContent>
    </Dialog>
  )
}

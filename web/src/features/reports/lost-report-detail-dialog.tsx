import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  SparklesIcon,
  FlagIcon,
  MapPinIcon,
  CalendarIcon,
  TagIcon,
  PaletteIcon,
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
import { ApiError, assetUrl } from '@/lib/api/client'
import { cn } from '@/lib/utils'

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

  const aiFacts = readAiFacts(detail?.parsedAttributesJson)

  return (
    <Dialog open={open} onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="max-h-[85vh] max-w-2xl overflow-y-auto">
        <DialogHeader>
          {/* Right padding keeps Edit clear of the dialog's own close button in the corner. */}
          <div className="flex items-center justify-between gap-4 pr-8">
            <DialogTitle className="flex items-center gap-2 text-xl">
              <TagIcon className="size-5 text-primary" />
              {detail ? [detail.primaryColor, detail.itemTypeName].filter(Boolean).join(' ') : 'Lost report'}
            </DialogTitle>
            {detail?.status === 'Active' && onEdit && (
              <Button size="sm" variant="outline" onClick={onEdit}>
                Edit Report
              </Button>
            )}
          </div>
          <DialogDescription>Your report, what the AI read from it, and any possible matches.</DialogDescription>
        </DialogHeader>

        {isReportPending || !detail ? (
          <div className="flex h-40 items-center justify-center">
            <Loader2Icon className="size-6 animate-spin text-muted-foreground" />
          </div>
        ) : (
          <div className="space-y-6 py-2">
            {/* The item: its photo (or its drawing), the student's words, and the facts. */}
            <div className="overflow-hidden rounded-xl border bg-muted/30">
              <ReportPhotos photos={detail.photos ?? []} itemType={detail.itemTypeName} category={detail.categoryName} />

              <div className="flex flex-col gap-4 p-4">
                <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
                  <Badge variant={detail.status === 'Active' ? 'default' : 'secondary'}>{detail.status}</Badge>
                  <span className="text-xs text-muted-foreground">Reported {formatDateTime(detail.createdAt)}</span>
                </div>

                <p className="text-sm leading-relaxed text-foreground">{detail.description}</p>

                {/* Label above value: side by side, long values wrapped into the next column. */}
                <dl className="grid grid-cols-1 gap-x-6 gap-y-3 text-sm sm:grid-cols-2">
                  <Fact icon={TagIcon} label="Category">{detail.categoryName} · {detail.itemTypeName}</Fact>
                  <Fact icon={MapPinIcon} label="Last seen">{detail.lastSeenLocationName}</Fact>
                  <Fact icon={CalendarIcon} label="Lost between" wide>
                    {formatDateTime(detail.estimatedLostFromAt)} – {formatDateTime(detail.estimatedLostToAt)}
                  </Fact>
                  {(detail.primaryColor || detail.secondaryColor) && (
                    <Fact icon={PaletteIcon} label="Colour">
                      {[detail.primaryColor, detail.secondaryColor].filter(Boolean).join(' and ')}
                    </Fact>
                  )}
                </dl>
              </div>
            </div>

            {/* What the description parser picked out, in words - never raw keys or nulls. */}
            <div className="rounded-xl border border-primary/20 bg-primary/5 p-4">
              <div className="flex items-center gap-2 text-sm font-medium text-primary">
                <SparklesIcon className="size-4" />
                What FoundU&apos;s AI picked out
              </div>
              {aiFacts.length > 0 ? (
                <>
                  <dl className="mt-3 grid grid-cols-1 gap-x-6 gap-y-2 text-sm sm:grid-cols-2">
                    {aiFacts.map(([label, value]) => (
                      <div key={label} className="flex flex-col">
                        <dt className="text-xs text-muted-foreground">{label}</dt>
                        <dd className="font-medium text-foreground">{value}</dd>
                      </div>
                    ))}
                  </dl>
                  <p className="mt-3 text-xs text-muted-foreground">Used to match your report against items handed in.</p>
                </>
              ) : (
                <p className="mt-2 text-xs text-muted-foreground">
                  Nothing extracted yet. Matching still works from the category, place and time you gave.
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
                        {match.matchScore != null && (
                          <span className="font-medium text-primary">
                            Match score: {Math.round(match.matchScore * 100)}%
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

function Fact({
  icon: Icon,
  label,
  wide,
  children,
}: {
  icon: typeof TagIcon
  label: string
  wide?: boolean
  children: React.ReactNode
}) {
  return (
    <div className={cn('flex items-start gap-2.5', wide && 'sm:col-span-2')}>
      <Icon className="mt-0.5 size-4 shrink-0 text-muted-foreground" aria-hidden="true" />
      <div className="flex min-w-0 flex-col">
        <dt className="text-xs text-muted-foreground">{label}</dt>
        <dd className="font-medium text-foreground">{children}</dd>
      </div>
    </div>
  )
}

/**
 * The photo, large, with thumbnails to switch when there are two. Report photos are stored on
 * the API, so their paths go through assetUrl - a bare "/uploads/..." asked the web server
 * instead and showed a broken image. No photo shows the item's drawing.
 */
function ReportPhotos({
  photos,
  itemType,
  category,
}: {
  photos: { id: string; url: string }[]
  itemType: string
  category: string
}) {
  const [shown, setShown] = useState(0)
  const current = photos[Math.min(shown, photos.length - 1)]

  if (!current) {
    return (
      <div className="flex h-36 items-center justify-center bg-muted/60 text-foreground/25">
        <span className="size-20" aria-hidden="true">
          <ItemIllustration itemType={itemType} category={category} />
        </span>
      </div>
    )
  }

  return (
    <div className="relative bg-black/5">
      <a href={assetUrl(current.url)} target="_blank" rel="noreferrer" title="Open the full photo">
        <img src={assetUrl(current.url)} alt={`Photo of the ${itemType.toLowerCase()}`} className="max-h-72 w-full object-contain" />
      </a>
      {photos.length > 1 && (
        <div className="absolute bottom-2 left-2 flex gap-1.5">
          {photos.map((photo, index) => (
            <button
              key={photo.id}
              type="button"
              onClick={() => setShown(index)}
              aria-label={`Photo ${index + 1}`}
              aria-pressed={index === shown}
              className={cn(
                'size-12 overflow-hidden rounded-md border-2 bg-background',
                index === shown ? 'border-primary' : 'border-transparent opacity-80',
              )}
            >
              <img src={assetUrl(photo.url)} alt="" className="size-full object-cover" />
            </button>
          ))}
        </div>
      )}
    </div>
  )
}

/** The parser's output as label/value pairs a person can read. Unknown keys and blanks are skipped. */
function readAiFacts(json: string | null | undefined): [string, string][] {
  if (!json) return []
  let raw: Record<string, unknown>
  try {
    raw = JSON.parse(json)
  } catch {
    return []
  }
  const text = (value: unknown) => (typeof value === 'string' && value.trim() && value !== 'null' ? value.trim() : null)
  const facts: [string, string][] = []
  const item = text(raw.itemType)
  if (item) facts.push(['Item', item])
  const colours = [text(raw.primaryColor), text(raw.secondaryColor)].filter(Boolean)
  if (colours.length) facts.push(['Colour', colours.join(' and ')])
  const features = Array.isArray(raw.identifyingFeatures)
    ? raw.identifyingFeatures.map(text).filter(Boolean)
    : []
  if (features.length) facts.push(['Distinctive', features.join(', ')])
  if (typeof raw.confidenceScore === 'number' && facts.length) {
    facts.push(['How sure it is', `${Math.round(raw.confidenceScore * 100)}%`])
  }
  return facts
}

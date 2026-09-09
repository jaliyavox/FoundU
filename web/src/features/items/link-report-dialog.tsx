import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Loader2Icon, LinkIcon, SearchIcon } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { createSuggestion } from '@/features/claims/claims-api'
import { formatDateTime } from '@/features/reports/reports-api'
import { ApiError } from '@/lib/api/client'
import { cn } from '@/lib/utils'
import { searchLostReports, type FoundReportDetail } from './items-api'

/**
 * Point a student at an item the desk is holding.
 *
 * This is the only way a student ever hears about a specific found item - there is no
 * browsable list of them, because that is how someone shops for something to claim. The
 * Matching Agent will write the same link with a confidence score; until then a person reads
 * the reports and decides.
 */
export function LinkReportDialog({
  item,
  open,
  onOpenChange,
}: {
  item: FoundReportDetail
  open: boolean
  onOpenChange: (open: boolean) => void
}) {
  const queryClient = useQueryClient()
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [selected, setSelected] = useState<string | null>(null)
  const [note, setNote] = useState('')

  const { data, isPending } = useQuery({
    queryKey: ['lost-reports-for-linking', { search }],
    queryFn: () => searchLostReports(1, 8, search),
    enabled: open,
  })

  const link = useMutation({
    mutationFn: () => createSuggestion(selected!, item.id, note.trim() || undefined),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['item-suggestions', item.id] })
      toast.success('The student will see it on their reports.')
      onOpenChange(false)
      setSelected(null)
      setNote('')
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : 'Could not reach the server.'),
  })

  function handleSearch(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSearch(searchInput)
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Suggest this {item.itemTypeName.toLowerCase()} to a student</DialogTitle>
          <DialogDescription>
            Pick the report it matches. They are told it might be theirs and can open a claim -
            they still have to answer the verification questions.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={handleSearch} className="flex gap-2">
          <div className="relative flex-1">
            <SearchIcon
              className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground"
              aria-hidden="true"
            />
            <Input
              value={searchInput}
              onChange={(event) => setSearchInput(event.target.value)}
              placeholder="Search active reports"
              className="pl-9"
              aria-label="Search active lost reports"
            />
          </div>
          <Button type="submit" variant="outline">
            Search
          </Button>
        </form>

        <div className="flex max-h-64 flex-col gap-2 overflow-y-auto">
          {isPending ? (
            <p className="py-6 text-center text-sm text-muted-foreground">Loading reports...</p>
          ) : (data?.items.length ?? 0) === 0 ? (
            <p className="py-6 text-center text-sm text-muted-foreground">
              No active reports match that search.
            </p>
          ) : (
            data!.items.map((report) => (
              <button
                key={report.id}
                type="button"
                onClick={() => setSelected(report.id)}
                aria-pressed={selected === report.id}
                className={cn(
                  'flex flex-col gap-1 rounded-xl border p-3 text-left transition-colors',
                  selected === report.id
                    ? 'border-brand-green bg-brand-green/10'
                    : 'border-foreground/10 hover:bg-muted/60',
                )}
              >
                <span className="text-sm font-medium">
                  {report.itemTypeName}
                  {report.primaryColor && (
                    <span className="pl-2 text-xs font-normal text-muted-foreground">
                      {report.primaryColor}
                    </span>
                  )}
                </span>
                <span className="line-clamp-2 text-xs text-muted-foreground">
                  {report.description}
                </span>
                <span className="text-xs text-muted-foreground">
                  Last seen {report.lastSeenLocationName} · {formatDateTime(report.estimatedLostFromAt)}
                </span>
              </button>
            ))
          )}
        </div>

        <div className="flex flex-col gap-2">
          <Label htmlFor="link-note">
            Note <span className="text-muted-foreground">- the student reads this</span>
          </Label>
          <Textarea
            id="link-note"
            rows={2}
            value={note}
            onChange={(event) => setNote(event.target.value)}
            placeholder="Came in from the library desk on Tuesday - have a look and tell us if it is yours."
          />
        </div>

        <DialogFooter>
          <Button
            type="button"
            variant="ghost"
            onClick={() => onOpenChange(false)}
            disabled={link.isPending}
          >
            Cancel
          </Button>
          <Button
            type="button"
            className="bg-brand-forest text-white hover:bg-brand-forest/90"
            onClick={() => link.mutate()}
            disabled={!selected || link.isPending}
          >
            {link.isPending ? (
              <Loader2Icon className="animate-spin" aria-hidden="true" />
            ) : (
              <LinkIcon aria-hidden="true" />
            )}
            Suggest it
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

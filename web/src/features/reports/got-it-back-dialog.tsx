import { useState } from 'react'
import { HandHeartIcon, Loader2Icon, SparklesIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import type { LostReportListItem } from './reports-api'

/**
 * "I found this" from the owner's side: the item is home, so the report closes.
 *
 * Deliberately not withdrawal. Withdrawing means giving up and nobody is thanked for it;
 * closing means it worked, and everyone who said they found it is credited for it.
 */
export function GotItBackDialog({
  report,
  onConfirm,
  onClose,
  isResolving,
}: {
  report: LostReportListItem | null
  onConfirm: (note: string) => void
  onClose: () => void
  isResolving: boolean
}) {
  const [note, setNote] = useState('')

  return (
    <Dialog
      open={report !== null}
      onOpenChange={open => {
        if (!open && !isResolving) {
          setNote('')
          onClose()
        }
      }}
    >
      <DialogContent className="sm:max-w-md">
        {report && (
          <div className="flex flex-col gap-4">
            <DialogHeader>
              <DialogTitle>You have your {report.itemTypeName.toLowerCase()} back?</DialogTitle>
              <DialogDescription>
                This closes the report. It leaves the lost feed, and the people who helped hear
                how it ended.
              </DialogDescription>
            </DialogHeader>

            <ul className="flex flex-col gap-3 rounded-xl border border-foreground/8 bg-muted/40 p-4">
              <li className="flex items-start gap-3">
                <HandHeartIcon className="mt-0.5 size-4 shrink-0 text-muted-foreground" aria-hidden="true" />
                <p className="text-sm text-muted-foreground">
                  Anyone who pressed "I found this" is told the item got home. Your messages
                  stay readable on the report.
                </p>
              </li>
              <li className="flex items-start gap-3">
                <SparklesIcon className="mt-0.5 size-4 shrink-0 text-muted-foreground" aria-hidden="true" />
                <p className="text-sm text-muted-foreground">
                  They each earn honor points for helping - the only way points are given out.
                </p>
              </li>
            </ul>

            <div className="flex flex-col gap-2">
              <Label htmlFor="got-it-back-note">Where did it turn up? (optional)</Label>
              <Textarea
                id="got-it-back-note"
                value={note}
                onChange={event => setNote(event.target.value)}
                maxLength={500}
                rows={2}
                placeholder="Handed in at the library desk."
                disabled={isResolving}
              />
            </div>

            <DialogFooter>
              <Button type="button" variant="ghost" onClick={onClose} disabled={isResolving}>
                Not yet
              </Button>
              <Button type="button" onClick={() => onConfirm(note.trim())} disabled={isResolving}>
                {isResolving && <Loader2Icon className="animate-spin" aria-hidden="true" />}
                Yes, close the report
              </Button>
            </DialogFooter>
          </div>
        )}
      </DialogContent>
    </Dialog>
  )
}

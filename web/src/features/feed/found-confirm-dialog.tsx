import { Loader2Icon, TriangleAlertIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import type { LostReportFeedItem } from './feed-api'
import { formatWindow } from './feed-api'

/**
 * The check before "I found this" is recorded.
 *
 * Pressing it notifies a real person that their lost thing has turned up, so a mis-tap or a
 * near-enough match costs them a false hope. This restates the identifying details - colour,
 * place, window - and asks the finder to match them against what is actually in their hand
 * before it goes any further.
 */
export function FoundConfirmDialog({
  item,
  open,
  onOpenChange,
  onConfirm,
  isPending,
}: {
  item: LostReportFeedItem
  open: boolean
  onOpenChange: (open: boolean) => void
  onConfirm: () => void
  isPending: boolean
}) {
  const firstName = item.postedByName.split(' ')[0]

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Are you sure you found this?</DialogTitle>
          <DialogDescription>
            {firstName} is told straight away that their {item.itemTypeName.toLowerCase()} has
            turned up. Check it against the details below first - this step is here so an
            accidental tap does not raise their hopes.
          </DialogDescription>
        </DialogHeader>

        <dl className="flex flex-col gap-2 rounded-xl border border-foreground/8 bg-muted/40 p-4 text-sm">
          <div className="flex gap-3">
            <dt className="w-24 shrink-0 text-muted-foreground">Item</dt>
            <dd>
              {item.itemTypeName}
              {item.primaryColor && `, ${item.primaryColor.toLowerCase()}`}
            </dd>
          </div>
          <div className="flex gap-3">
            <dt className="w-24 shrink-0 text-muted-foreground">Last seen</dt>
            <dd>{item.lastSeenLocationName}</dd>
          </div>
          <div className="flex gap-3">
            <dt className="w-24 shrink-0 text-muted-foreground">Lost between</dt>
            <dd>{formatWindow(item.estimatedLostFromAt, item.estimatedLostToAt)}</dd>
          </div>
          <div className="flex gap-3">
            <dt className="w-24 shrink-0 text-muted-foreground">Described as</dt>
            <dd className="text-pretty">{item.description}</dd>
          </div>
        </dl>

        <p className="flex items-start gap-2 text-xs text-muted-foreground">
          <TriangleAlertIcon className="mt-0.5 size-3.5 shrink-0" aria-hidden="true" />
          If it only looks similar, close this. A near-enough match sends the wrong person
          across campus.
        </p>

        <DialogFooter>
          <Button
            type="button"
            variant="ghost"
            onClick={() => onOpenChange(false)}
            disabled={isPending}
          >
            Not sure yet
          </Button>
          <Button
            type="button"
            onClick={onConfirm}
            disabled={isPending}
            className="bg-brand-forest text-white hover:bg-brand-forest/90"
          >
            {isPending && <Loader2Icon className="animate-spin" aria-hidden="true" />}
            Yes, continue
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

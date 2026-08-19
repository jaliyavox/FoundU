import { EyeOffIcon, Loader2Icon, MessageSquareOffIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import type { LostReportListItem } from './reports-api'

/**
 * Withdrawal confirmation. Withdrawing cannot be undone from the UI - there is no
 * "reinstate" on a lost report - so the two things it costs are spelled out before the
 * click, not discovered afterwards.
 */
export function WithdrawDialog({
  report,
  onConfirm,
  onClose,
  isWithdrawing,
}: {
  report: LostReportListItem | null
  onConfirm: () => void
  onClose: () => void
  isWithdrawing: boolean
}) {
  return (
    <Dialog open={report !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-md">
        {report && (
          <div className="flex flex-col gap-4">
            <DialogHeader>
              <DialogTitle>Withdraw this {report.itemTypeName.toLowerCase()} report?</DialogTitle>
              <DialogDescription>
                This cannot be undone. If you lose it again you will need to post a new report.
              </DialogDescription>
            </DialogHeader>

            <ul className="flex flex-col gap-3 rounded-xl border border-foreground/8 bg-muted/40 p-4">
              <li className="flex items-start gap-3">
                <EyeOffIcon
                  className="mt-0.5 size-4 shrink-0 text-muted-foreground"
                  aria-hidden="true"
                />
                <p className="text-sm text-muted-foreground">
                  The notice disappears from the lost feed, so nobody browsing campus posts can
                  see it any more.
                </p>
              </li>
              <li className="flex items-start gap-3">
                <MessageSquareOffIcon
                  className="mt-0.5 size-4 shrink-0 text-muted-foreground"
                  aria-hidden="true"
                />
                <p className="text-sm text-muted-foreground">
                  The report closes to messages. Anyone who finds your item can no longer reach
                  you through it, and the conversation ends there.
                </p>
              </li>
            </ul>

            <DialogFooter>
              <Button type="button" variant="ghost" onClick={onClose} disabled={isWithdrawing}>
                Keep the report
              </Button>
              <Button type="button" variant="destructive" onClick={onConfirm} disabled={isWithdrawing}>
                {isWithdrawing && <Loader2Icon className="animate-spin" aria-hidden="true" />}
                Withdraw report
              </Button>
            </DialogFooter>
          </div>
        )}
      </DialogContent>
    </Dialog>
  )
}

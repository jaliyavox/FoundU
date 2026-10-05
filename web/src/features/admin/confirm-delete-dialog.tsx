import type { ReactNode } from 'react'
import { Loader2Icon, Trash2Icon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'

/**
 * The one confirmation every admin deletion goes through. Deleting cannot be undone from the
 * app, so it always takes a second, deliberate click on a button that says what will happen.
 */
export function ConfirmDeleteDialog({
  open,
  title,
  children,
  confirmLabel,
  pending,
  onConfirm,
  onClose,
}: {
  open: boolean
  title: string
  children: ReactNode
  confirmLabel: string
  pending: boolean
  onConfirm: () => void
  onClose: () => void
}) {
  return (
    <Dialog open={open} onOpenChange={(next) => !next && !pending && onClose()}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription render={<div className="flex flex-col gap-2" />}>{children}</DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={pending}>
            Cancel
          </Button>
          <Button variant="destructive" onClick={onConfirm} disabled={pending}>
            {pending ? <Loader2Icon className="animate-spin" aria-hidden="true" /> : <Trash2Icon aria-hidden="true" />}
            {confirmLabel}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

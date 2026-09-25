import { useEffect, useState, type FormEvent } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Loader2Icon, ShieldOffIcon } from 'lucide-react'
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
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { suspendUser, type AdminUser } from './admin-api'
import { ApiError } from '@/lib/api/client'

/**
 * Suspension confirmation. The reason is required by the API, not decoration - it is stored
 * against the account and shown in the table long after the admin who set it has moved on.
 */
export function SuspendDialog({ user, onClose }: { user: AdminUser | null; onClose: () => void }) {
  const queryClient = useQueryClient()
  const [reason, setReason] = useState('')
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({})

  useEffect(() => {
    if (user) {
      setReason('')
      setFieldErrors({})
    }
  }, [user])

  const mutation = useMutation({
    mutationFn: () => suspendUser(user!.id, reason),
    onSuccess: (updated) => {
      queryClient.invalidateQueries({ queryKey: ['admin-users'] })
      queryClient.invalidateQueries({ queryKey: ['admin-user-stats'] })
      toast.success(`${updated.fullName} has been suspended and signed out.`)
      onClose()
    },
    onError: (error) => {
      if (error instanceof ApiError) {
        setFieldErrors(error.fieldErrors)
        // 403 covers "administrators cannot be suspended", which has no field to bind to.
        if (Object.keys(error.fieldErrors).length === 0) toast.error(error.message)
      } else {
        toast.error('Could not reach the server.')
      }
    },
  })

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setFieldErrors({})
    mutation.mutate()
  }

  return (
    <Dialog open={user !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-md">
        {user && (
          <form onSubmit={handleSubmit} className="flex flex-col gap-4">
            <DialogHeader>
              <DialogTitle>Suspend {user.fullName}?</DialogTitle>
              <DialogDescription>
                They will be signed out immediately and cannot sign in again until reinstated.
                Their reports stay on the system.
              </DialogDescription>
            </DialogHeader>

            <div className="flex flex-col gap-2">
              <Label htmlFor="suspend-reason">Reason</Label>
              <Textarea
                id="suspend-reason"
                rows={3}
                required
                value={reason}
                onChange={(event) => setReason(event.target.value)}
                placeholder="Repeatedly posting reports for items that were never lost."
                aria-invalid={Boolean(fieldErrors.Reason)}
                aria-describedby={fieldErrors.Reason ? 'suspend-reason-error' : undefined}
              />
              {fieldErrors.Reason && (
                <p id="suspend-reason-error" className="text-sm text-destructive">
                  {fieldErrors.Reason.join(' ')}
                </p>
              )}
              <p className="text-xs text-muted-foreground">
                Recorded against the account, with your name and the date.
              </p>
            </div>

            <DialogFooter>
              <Button type="button" variant="ghost" onClick={onClose} disabled={mutation.isPending}>
                Cancel
              </Button>
              <Button type="submit" variant="destructive" disabled={mutation.isPending}>
                {mutation.isPending ? (
                  <Loader2Icon className="animate-spin" aria-hidden="true" />
                ) : (
                  <ShieldOffIcon aria-hidden="true" />
                )}
                Suspend account
              </Button>
            </DialogFooter>
          </form>
        )}
      </DialogContent>
    </Dialog>
  )
}

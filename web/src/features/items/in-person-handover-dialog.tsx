import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Loader2Icon, PackageCheckIcon, SearchIcon } from 'lucide-react'
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
import { findStudents, handOverInPerson, type DeskStudent } from '@/features/claims/claims-api'
import { ApiError } from '@/lib/api/client'
import { cn } from '@/lib/utils'
import type { FoundReportDetail } from './items-api'

/**
 * The owner is standing at the desk. Staff find their account, ask questions face to face
 * (the hidden detail is on this page for them to check against), note the answers, check the
 * student ID, and hand the item over - recorded as an approved, collected claim, with the usual
 * receipt to the owner and thanks to the finder. For owners who never reported it lost, or who
 * would rather not do it through the app.
 */
export function InPersonHandoverDialog({
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
  const [student, setStudent] = useState<DeskStudent | null>(null)
  const [notes, setNotes] = useState('')
  const [idChecked, setIdChecked] = useState(false)

  const results = useQuery({
    queryKey: ['desk-students', search],
    queryFn: () => findStudents(search),
    enabled: search.trim().length >= 2,
  })

  const handOver = useMutation({
    mutationFn: () => handOverInPerson({
      foundReportId: item.id,
      studentId: student!.id,
      verificationNotes: notes.trim(),
      ownerIdChecked: idChecked,
    }),
    onSuccess: () => {
      for (const key of ['found-item', 'found-items', 'claims', 'item-suggestions']) {
        queryClient.invalidateQueries({ queryKey: [key] })
      }
      toast.success(`Handed over to ${student!.fullName}. They have a receipt.`)
      close(false)
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : 'Could not reach the server.'),
  })

  function close(next: boolean) {
    if (!next) {
      setSearchInput('')
      setSearch('')
      setStudent(null)
      setNotes('')
      setIdChecked(false)
    }
    onOpenChange(next)
  }

  function lookUp(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setStudent(null)
    setSearch(searchInput.trim())
  }

  const ready = student !== null && notes.trim().length >= 10 && idChecked

  return (
    <Dialog open={open} onOpenChange={(next) => !handOver.isPending && close(next)}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Owner here in person</DialogTitle>
          <DialogDescription>
            Ask them about the {item.itemTypeName.toLowerCase()} without showing it - the hidden detail
            on this page is what their answers should match. Hand it over only when they do.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={lookUp} className="flex flex-col gap-2">
          <Label htmlFor="desk-student">1. Find their FoundU account</Label>
          <div className="flex gap-2">
            <Input
              id="desk-student"
              value={searchInput}
              onChange={(event) => setSearchInput(event.target.value)}
              placeholder="Email, student number or name"
            />
            <Button type="submit" variant="outline" disabled={searchInput.trim().length < 2}>
              <SearchIcon aria-hidden="true" />
              Find
            </Button>
          </div>
        </form>

        {search && (
          <div className="flex max-h-44 flex-col gap-1.5 overflow-y-auto">
            {results.isPending ? (
              <p className="text-sm text-muted-foreground">Searching…</p>
            ) : (results.data?.length ?? 0) === 0 ? (
              <p className="text-sm text-muted-foreground">
                No student account matches. They can register in a minute on their phone - then search again.
              </p>
            ) : (
              results.data!.map((s) => (
                <button
                  key={s.id}
                  type="button"
                  onClick={() => setStudent(s)}
                  aria-pressed={student?.id === s.id}
                  className={cn(
                    'flex flex-col rounded-lg border p-2.5 text-left text-sm transition-colors',
                    student?.id === s.id ? 'border-brand-green bg-brand-green/10' : 'border-foreground/10 hover:bg-muted/60',
                  )}
                >
                  <span className="font-medium">{s.fullName}</span>
                  <span className="text-xs text-muted-foreground">
                    {[s.studentNumber, s.email].filter(Boolean).join(' · ')}
                  </span>
                </button>
              ))
            )}
          </div>
        )}

        <div className="flex flex-col gap-2">
          <Label htmlFor="desk-notes">2. What you asked, and what they answered</Label>
          <Textarea
            id="desk-notes"
            rows={3}
            maxLength={500}
            value={notes}
            onChange={(event) => setNotes(event.target.value)}
            placeholder="Asked what is inside - said a library card with her name. Matches."
          />
          <p className="text-xs text-muted-foreground">Kept on the claim's history for staff; the owner never sees it.</p>
        </div>

        <label className="flex items-start gap-2 text-sm">
          <input
            type="checkbox"
            className="mt-0.5"
            checked={idChecked}
            onChange={(event) => setIdChecked(event.target.checked)}
          />
          <span>3. I checked their student ID{student ? ` - it says ${student.fullName}` : ''}.</span>
        </label>

        <DialogFooter>
          <Button variant="outline" onClick={() => close(false)} disabled={handOver.isPending}>
            Cancel
          </Button>
          <Button
            className="bg-brand-forest text-white hover:bg-brand-forest/90"
            disabled={!ready || handOver.isPending}
            onClick={() => handOver.mutate()}
          >
            {handOver.isPending ? <Loader2Icon className="animate-spin" aria-hidden="true" /> : <PackageCheckIcon aria-hidden="true" />}
            Verify and hand over
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

import { useEffect, useRef, useState, type FormEvent } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useMutation, useQuery } from '@tanstack/react-query'
import {
  CheckIcon,
  HashIcon,
  Loader2Icon,
  PackageCheckIcon,
  SearchIcon,
  ShieldAlertIcon,
  UserRoundIcon,
} from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { DashboardPanel, PanelDivider } from '@/components/layout/dashboard-panel'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { FormSelect } from '@/features/reports/form-select'
import { getStorageLocations } from '@/features/reports/reports-api'
import { ApiError } from '@/lib/api/client'
import {
  lookupHandover,
  receiveHandover,
  releaseHandover,
  type HandoverLookup,
} from '@/features/feed/handover-api'

/**
 * The desk's side of a handover: one code, typed in twice.
 *
 * Once when the finder arrives with the item, once when its owner comes for it. The second
 * time the desk has to say it checked who the collector is - a code says which item, never
 * which person, and that check is the only thing standing between the two.
 */
export function HandoverDeskPage() {
  // Sent here by the code box on Found items, with the code the finder quoted.
  const [params, setParams] = useSearchParams()
  const carried = params.get('code')?.replace(/\D/g, '').slice(0, 6) ?? ''
  const [code, setCode] = useState(carried)
  const [found, setFound] = useState<HandoverLookup | null>(null)

  const lookup = useMutation({
    mutationFn: () => lookupHandover(code),
    onSuccess: setFound,
    onError: error => {
      setFound(null)
      toast.error(error instanceof ApiError ? error.message : 'Could not look that up.')
    },
  })

  // Looked up on arrival. Scheduled and cancelled on cleanup, so React's development double
  // mount cannot leave the lookup's observer detached (see the Ask FoundU page).
  const looked = useRef(false)
  useEffect(() => {
    if (carried.length !== 6 || looked.current) return
    const timer = setTimeout(() => {
      looked.current = true
      lookup.mutate()
      setParams({}, { replace: true })
    }, 0)
    return () => clearTimeout(timer)
  }, [carried, lookup, setParams])

  function submit(event: FormEvent) {
    event.preventDefault()
    if (code.replace(/\s/g, '').length === 6 && !lookup.isPending) lookup.mutate()
  }

  return (
    <section className="mx-auto flex w-full max-w-2xl flex-col gap-6">
      <div>
        <p className="text-sm font-medium text-brand-green">Front desk</p>
        <h1 className="pt-1 text-2xl font-semibold tracking-tight">Handover code</h1>
        <p className="max-w-xl pt-2 text-sm text-muted-foreground">
          A student brings an item in, or comes to collect one. Type the six digits they quote.
        </p>
      </div>

      <DashboardPanel>
        <form onSubmit={submit} className="flex flex-col gap-4">
          <Label htmlFor="handover-code">Code</Label>
          <div className="flex items-center gap-2">
            <div className="relative flex-1">
              <HashIcon className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden="true" />
              <Input
                id="handover-code"
                value={code}
                onChange={event => setCode(event.target.value)}
                inputMode="numeric"
                autoComplete="off"
                placeholder="483 921"
                className="pl-9 font-mono text-lg tracking-[0.2em] tabular-nums"
              />
            </div>
            <Button type="submit" disabled={lookup.isPending || code.replace(/\s/g, '').length !== 6}>
              {lookup.isPending ? <Loader2Icon className="animate-spin" aria-hidden="true" /> : <SearchIcon aria-hidden="true" />}
              Look up
            </Button>
          </div>
          <p className="text-xs text-muted-foreground">
            A code that has been used, or never existed, looks the same from here.
          </p>
        </form>
      </DashboardPanel>

      {found && <HandoverCard lookup={found} code={code} onChanged={setFound} />}
    </section>
  )
}

function HandoverCard({
  lookup,
  code,
  onChanged,
}: {
  lookup: HandoverLookup
  code: string
  onChanged: (next: HandoverLookup) => void
}) {
  const [storageLocationId, setStorageLocationId] = useState('')
  const [note, setNote] = useState('')
  const [idChecked, setIdChecked] = useState(false)

  const storage = useQuery({
    queryKey: ['storage-locations'],
    queryFn: getStorageLocations,
    enabled: lookup.nextStep === 'receive',
  })

  const receive = useMutation({
    mutationFn: () => receiveHandover(code, storageLocationId, note.trim() || undefined),
    onSuccess: next => {
      onChanged(next)
      setNote('')
      toast.success('Logged. The owner has been told where it is.')
    },
    onError: error => toast.error(error instanceof ApiError ? error.message : 'Could not log the item.'),
  })

  const release = useMutation({
    mutationFn: () => releaseHandover(code, note.trim() || undefined),
    onSuccess: next => {
      onChanged(next)
      setNote('')
      toast.success('Released. The report is closed and the finder has been thanked.')
    },
    onError: error => toast.error(error instanceof ApiError ? error.message : 'Could not release the item.'),
  })

  return (
    <DashboardPanel className="flex flex-col gap-5">
      <div>
        <h2 className="text-xl font-semibold tracking-tight">
          {[lookup.primaryColor, lookup.itemTypeName].filter(Boolean).join(' ')}
        </h2>
        <p className="pt-1 text-sm text-muted-foreground">
          {lookup.categoryName} · lost at {lookup.lastSeenLocationName}
        </p>
        <p className="pt-3 text-sm">{lookup.description}</p>
      </div>

      <PanelDivider />

      <dl className="grid gap-3 sm:grid-cols-2">
        <div>
          <dt className="text-xs text-muted-foreground">Brought in by</dt>
          <dd className="text-sm">{lookup.finderName}</dd>
        </div>
        <div>
          <dt className="text-xs text-muted-foreground">Belongs to</dt>
          <dd className="text-sm">
            {lookup.ownerName}
            {lookup.ownerStudentNumber && ` · ${lookup.ownerStudentNumber}`}
          </dd>
        </div>
        {lookup.storageLocationName && (
          <div>
            <dt className="text-xs text-muted-foreground">Kept at</dt>
            <dd className="text-sm">{lookup.storageLocationName}</dd>
          </div>
        )}
      </dl>

      <PanelDivider />

      {lookup.nextStep === 'receive' ? (
        <div className="flex flex-col gap-4">
          <p className="flex items-start gap-2 text-sm">
            <PackageCheckIcon className="mt-0.5 size-4 shrink-0 text-brand-forest dark:text-brand-sage" aria-hidden="true" />
            The finder is here with it. Log where it is going and the owner is told straight away.
          </p>

          <div className="flex flex-col gap-2">
            <Label htmlFor="handover-storage">Where is it being kept?</Label>
            <FormSelect
              id="handover-storage"
              value={storageLocationId}
              onValueChange={setStorageLocationId}
              options={(storage.data ?? []).map(location => ({ value: location.id, label: location.name }))}
              placeholder={storage.isPending ? 'Loading' : 'Choose a shelf'}
            />
          </div>

          <div className="flex flex-col gap-2">
            <Label htmlFor="handover-note">Note (optional)</Label>
            <Textarea
              id="handover-note"
              rows={2}
              maxLength={200}
              value={note}
              onChange={event => setNote(event.target.value)}
              placeholder="Left in the green crate behind the counter."
            />
          </div>

          <Button
            className="self-start"
            disabled={!storageLocationId || receive.isPending}
            onClick={() => receive.mutate()}
          >
            {receive.isPending && <Loader2Icon className="animate-spin" aria-hidden="true" />}
            Take the item in
          </Button>
        </div>
      ) : lookup.nextStep === 'release' ? (
        <div className="flex flex-col gap-4">
          <p className="flex items-start gap-2 rounded-xl border border-amber-500/30 bg-amber-50 p-3 text-sm text-amber-900 dark:bg-amber-950/30 dark:text-amber-200">
            <ShieldAlertIcon className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
            <span>
              The code says which item, not who this person is. Check their student ID against
              <strong className="px-1">{lookup.ownerName}</strong>
              before you hand anything over.
            </span>
          </p>

          <label className="flex items-start gap-3 text-sm">
            <input
              type="checkbox"
              checked={idChecked}
              onChange={event => setIdChecked(event.target.checked)}
              className="mt-0.5 size-4 accent-[var(--color-brand-forest)]"
            />
            <span className="flex items-center gap-1.5">
              <UserRoundIcon className="size-3.5 text-muted-foreground" aria-hidden="true" />
              I checked their student ID and the name matches
            </span>
          </label>

          <div className="flex flex-col gap-2">
            <Label htmlFor="release-note">What you checked (optional)</Label>
            <Textarea
              id="release-note"
              rows={2}
              maxLength={200}
              value={note}
              onChange={event => setNote(event.target.value)}
              placeholder="Student ID IT26001234, name matched."
            />
          </div>

          <Button
            className="self-start"
            disabled={!idChecked || release.isPending}
            onClick={() => release.mutate()}
          >
            {release.isPending && <Loader2Icon className="animate-spin" aria-hidden="true" />}
            Release to the owner
          </Button>
        </div>
      ) : (
        <p className="flex items-center gap-2 text-sm">
          <CheckIcon className="size-4 text-brand-forest dark:text-brand-sage" aria-hidden="true" />
          Done - this one went home. The code no longer works.
        </p>
      )}
    </DashboardPanel>
  )
}

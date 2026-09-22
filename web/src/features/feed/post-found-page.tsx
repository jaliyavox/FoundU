import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useMutation, useQuery } from '@tanstack/react-query'
import { ArrowLeftIcon, HandIcon, HashIcon, Loader2Icon, RotateCwIcon } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { DashboardPanel, PanelDivider } from '@/components/layout/dashboard-panel'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { Textarea } from '@/components/ui/textarea'
import { DateTimePicker } from '@/features/reports/date-time-picker'
import { FormSelect } from '@/features/reports/form-select'
import { defaultWindow, getCategories, getLocations, toUtcIso } from '@/features/reports/reports-api'
import { ApiError } from '@/lib/api/client'
import { displayCode, postFound } from './feed-api'

/**
 * "I found something." One short form: what, where, when, and a line the owner would
 * recognise. Nothing that proves ownership - that is the desk's to write when the item is
 * handed in, and the form says so, because a finder who lists every detail out of
 * helpfulness hands a fraudulent claimant the answers.
 */
export function PostFoundPage() {
  const navigate = useNavigate()

  const categories = useQuery({ queryKey: ['categories'], queryFn: getCategories })
  const locations = useQuery({ queryKey: ['locations'], queryFn: getLocations })

  const [categoryId, setCategoryId] = useState('')
  const [itemTypeId, setItemTypeId] = useState('')
  const [foundLocationId, setFoundLocationId] = useState('')
  const [description, setDescription] = useState('')
  const [primaryColor, setPrimaryColor] = useState('')
  const [foundAt, setFoundAt] = useState(() => defaultWindow().to)
  const [lostCode, setLostCode] = useState('')
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({})
  const [posted, setPosted] = useState<{ code: string | null } | null>(null)

  const create = useMutation({
    mutationFn: () =>
      postFound({
        categoryId,
        itemTypeId,
        foundLocationId,
        description: description.trim(),
        primaryColor: primaryColor.trim() || undefined,
        foundAt: toUtcIso(foundAt),
        lostReportHandInCode: lostCode.replace(/\s/g, '') || undefined,
      }),
    onSuccess: (post) => {
      setPosted({ code: post.handInCode })
      toast.success('Posted. Thank you.')
    },
    onError: (error) => {
      if (error instanceof ApiError) {
        setFieldErrors(error.fieldErrors)
        if (Object.keys(error.fieldErrors).length === 0) toast.error(error.message)
      } else {
        toast.error('Could not reach the server.')
      }
    },
  })

  if (categories.isPending || locations.isPending) {
    return (
      <section className="mx-auto flex w-full max-w-2xl flex-col gap-6">
        <Skeleton className="h-7 w-56" />
        <DashboardPanel className="flex flex-col gap-4">
          {Array.from({ length: 4 }).map((_, i) => (
            <Skeleton key={i} className="h-9 w-full" />
          ))}
        </DashboardPanel>
      </section>
    )
  }

  if (categories.isError || locations.isError) {
    return (
      <DashboardPanel role="alert" className="mx-auto flex w-full max-w-2xl flex-col items-start gap-3">
        <p className="font-heading text-base font-medium">Could not load the form</p>
        <Button variant="outline" onClick={() => { categories.refetch(); locations.refetch() }}>
          <RotateCwIcon aria-hidden="true" />
          Try again
        </Button>
      </DashboardPanel>
    )
  }

  if (posted) {
    return (
      <section className="mx-auto flex w-full max-w-2xl flex-col gap-6">
        <DashboardPanel className="flex flex-col gap-4">
          <h1 className="text-2xl font-semibold tracking-tight">Posted. Now walk it to a desk.</h1>
          <p className="text-sm text-muted-foreground">
            It is on the found feed so the owner can spot it, and we are checking it against open
            reports. Nobody can claim it until a desk has it - so the sooner it gets there, the sooner
            it gets home.
          </p>
          {posted.code && (
            <div className="flex items-center justify-between gap-4 rounded-xl bg-brand-forest px-4 py-3 text-white">
              <div>
                <p className="text-xs text-white/70">Quote this at the desk</p>
                <p className="font-mono text-2xl font-semibold tracking-[0.2em] tabular-nums">{displayCode(posted.code)}</p>
              </div>
              <HashIcon className="size-6 text-white/60" aria-hidden="true" />
            </div>
          )}
          <div className="flex gap-3">
            <Button nativeButton={false} render={<Link to="/feed" />}>See it on the feed</Button>
            <Button variant="ghost" onClick={() => navigate(-1)}>Done</Button>
          </div>
        </DashboardPanel>
      </section>
    )
  }

  const itemTypes = categories.data.find((c) => c.id === categoryId)?.itemTypes ?? []

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setFieldErrors({})
    create.mutate()
  }

  return (
    <section className="mx-auto flex w-full max-w-2xl flex-col gap-6">
      <Button variant="ghost" size="sm" className="self-start text-muted-foreground" onClick={() => navigate(-1)}>
        <ArrowLeftIcon aria-hidden="true" />
        Back
      </Button>

      <DashboardPanel>
        <div className="flex items-center gap-3">
          <span className="flex size-10 items-center justify-center rounded-xl bg-brand-mist text-brand-forest">
            <HandIcon className="size-5" aria-hidden="true" />
          </span>
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">Found something?</h1>
            <p className="pt-0.5 text-sm text-muted-foreground">Post it so the owner can spot it, then hand it in at any desk.</p>
          </div>
        </div>
      </DashboardPanel>

      <form onSubmit={handleSubmit} className="flex flex-col gap-6">
        <DashboardPanel className="flex flex-col gap-5">
          <div className="grid gap-5 sm:grid-cols-2">
            <Field label="What is it?" htmlFor="category" errors={fieldErrors.CategoryId}>
              <FormSelect id="category" value={categoryId} onValueChange={(v) => { setCategoryId(v); setItemTypeId('') }}
                options={categories.data.map((c) => ({ value: c.id, label: c.name }))} placeholder="Category" />
            </Field>
            <Field label="Item type" htmlFor="item-type" errors={fieldErrors.ItemTypeId}>
              <FormSelect id="item-type" value={itemTypeId} onValueChange={setItemTypeId}
                options={itemTypes.map((t) => ({ value: t.id, label: t.name }))} placeholder={categoryId ? 'Choose' : 'Pick a category first'} disabled={!categoryId} />
            </Field>
            <Field label="Where did you find it?" htmlFor="location" errors={fieldErrors.FoundLocationId}>
              <FormSelect id="location" value={foundLocationId} onValueChange={setFoundLocationId}
                options={locations.data.map((l) => ({ value: l.id, label: l.name }))} placeholder="Location" />
            </Field>
            <Field label="When" htmlFor="found-at" errors={fieldErrors.FoundAt}>
              <DateTimePicker id="found-at" value={foundAt} onChange={setFoundAt} />
            </Field>
            <Field label="Main colour" htmlFor="colour" errors={fieldErrors.PrimaryColor}>
              <Input id="colour" value={primaryColor} onChange={(e) => setPrimaryColor(e.target.value)} placeholder="Grey" />
            </Field>
          </div>

          <PanelDivider />

          <Field
            label="A line the owner would recognise"
            htmlFor="description"
            errors={fieldErrors.Description}
            hint="What it is and roughly where. Leave out anything that would prove it is theirs - a name inside, what is in the pockets. The desk records that, so only the real owner can answer for it."
          >
            <Textarea id="description" rows={3} required value={description} onChange={(e) => setDescription(e.target.value)}
              placeholder="Grey water bottle with stickers, on a bench outside the gym." />
          </Field>
        </DashboardPanel>

        <DashboardPanel className="flex flex-col gap-3">
          <Field
            label="Did you spot the owner's post?"
            htmlFor="lost-code"
            errors={fieldErrors.LostReportHandInCode}
            hint="If you already found the matching lost post on the feed, type its six-digit code and the owner is told straight away."
          >
            <Input id="lost-code" value={lostCode} onChange={(e) => setLostCode(e.target.value.replace(/[^\d\s]/g, '').slice(0, 7))}
              inputMode="numeric" placeholder="483 921" className="max-w-48 font-mono text-lg tracking-[0.2em]" />
          </Field>
        </DashboardPanel>

        <div className="flex justify-end gap-3">
          <Button variant="ghost" onClick={() => navigate(-1)}>Cancel</Button>
          <Button type="submit" disabled={create.isPending} className="bg-brand-forest text-white hover:bg-brand-forest/90">
            {create.isPending && <Loader2Icon className="animate-spin" aria-hidden="true" />}
            Post it
          </Button>
        </div>
      </form>
    </section>
  )
}

function Field({ label, htmlFor, errors, hint, children }: { label: string; htmlFor: string; errors?: string[]; hint?: string; children: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-2">
      <Label htmlFor={htmlFor}>{label}</Label>
      {children}
      {hint && !errors && <p className="text-xs text-pretty text-muted-foreground">{hint}</p>}
      {errors && <p className="text-sm text-destructive">{errors.join(' ')}</p>}
    </div>
  )
}

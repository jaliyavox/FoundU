import { useMemo, useState, type ComponentType } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeftIcon, ArrowRightIcon, CheckIcon, Loader2Icon, RotateCwIcon } from 'lucide-react'
import { toast } from 'sonner'
import {
  CollectIllustration,
  MatchIllustration,
  ReportIllustration,
  SearchSceneIllustration,
} from '@/components/illustrations'
import { Button } from '@/components/ui/button'
import { DashboardPanel } from '@/components/layout/dashboard-panel'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { Textarea } from '@/components/ui/textarea'
import { DateTimePicker } from './date-time-picker'
import { FormSelect } from './form-select'
import { PhotoPicker } from './photo-picker'
import { WizardSteps } from './wizard-steps'
import {
  createLostReport,
  defaultWindow,
  uploadLostReportPhotos,
  getCategories,
  getLocations,
  toUtcIso,
} from './reports-api'
import { ApiError } from '@/lib/api/client'

/**
 * Report a lost item, as a four-step wizard.
 *
 * One long form asked for everything at once and gave the API the first chance to object.
 * Splitting it means each step can check its own fields before letting you move on, so a
 * mistake surfaces where it was made rather than at the end.
 */

interface StepDefinition {
  label: string
  title: string
  body: string
  illustration: ComponentType<{ className?: string }>
}

const STEPS: StepDefinition[] = [
  {
    label: 'Item',
    title: 'What did you lose?',
    body: 'Start with the kind of thing it is. This narrows the search before anything else.',
    illustration: ReportIllustration,
  },
  {
    label: 'Place & time',
    title: 'Where and when?',
    body: 'A rough window is fine. We compare it against when things were handed in.',
    illustration: SearchSceneIllustration,
  },
  {
    label: 'Details',
    title: 'Describe it',
    body: 'The details only you would notice are what make a match convincing.',
    illustration: MatchIllustration,
  },
  {
    label: 'Review',
    title: 'Ready to post?',
    body: 'Your report appears on the public feed so anyone who finds it can recognise it.',
    illustration: CollectIllustration,
  },
]

export function ReportLostPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const initialWindow = useMemo(defaultWindow, [])

  const categories = useQuery({ queryKey: ['reference', 'categories'], queryFn: getCategories })
  const locations = useQuery({ queryKey: ['reference', 'locations'], queryFn: getLocations })

  const [step, setStep] = useState(0)
  const [direction, setDirection] = useState<'forward' | 'back'>('forward')

  const [categoryId, setCategoryId] = useState('')
  const [itemTypeId, setItemTypeId] = useState('')
  const [locationId, setLocationId] = useState('')
  const [description, setDescription] = useState('')
  const [primaryColor, setPrimaryColor] = useState('')
  const [from, setFrom] = useState(initialWindow.from)
  const [to, setTo] = useState(initialWindow.to)
  const [photos, setPhotos] = useState<File[]>([])
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({})

  const category = categories.data?.find((c) => c.id === categoryId)
  const itemTypes = category?.itemTypes ?? []
  const itemType = itemTypes.find((t) => t.id === itemTypeId)
  const location = locations.data?.find((l) => l.id === locationId)

  const mutation = useMutation({
    mutationFn: async (input: Parameters<typeof createLostReport>[0]) => {
      const created = await createLostReport(input)

      if (photos.length > 0) {
        // The report is already saved. A failed upload must not read as a failed report.
        try {
          await uploadLostReportPhotos(created.id, photos)
        } catch {
          toast.warning('Report posted, but the photos could not be uploaded.')
        }
      }

      return created
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['my-lost-reports'] })
      queryClient.invalidateQueries({ queryKey: ['lost-feed'] })
      toast.success('Report posted. We will tell you if something matching turns up.')
      navigate('/my-reports')
    },
    onError: (error) => {
      if (error instanceof ApiError) {
        setFieldErrors(error.fieldErrors)
        toast.error(error.message)
        // Send them back to the step that owns the rejected field.
        const keys = Object.keys(error.fieldErrors)
        if (keys.some((k) => k.startsWith('Category') || k.startsWith('ItemType'))) setStep(0)
        else if (keys.some((k) => k.includes('Location') || k.startsWith('Estimated'))) setStep(1)
        else if (keys.some((k) => k.startsWith('Description') || k.includes('Color'))) setStep(2)
      } else {
        toast.error('Could not reach the server.')
      }
    },
  })

  // Each step gates the next, so a problem shows up where it was made.
  const stepIsValid = [
    Boolean(categoryId && itemTypeId),
    Boolean(locationId && from && to && new Date(from) <= new Date(to)),
    description.trim().length >= 10,
    true,
  ][step]

  function go(next: number) {
    setDirection(next > step ? 'forward' : 'back')
    setStep(next)
  }

  function handleSubmit() {
    setFieldErrors({})
    mutation.mutate({
      categoryId,
      itemTypeId,
      lastSeenLocationId: locationId,
      description,
      primaryColor: primaryColor.trim() || undefined,
      estimatedLostFromAt: toUtcIso(from),
      estimatedLostToAt: toUtcIso(to),
    })
  }

  const referenceLoading = categories.isPending || locations.isPending
  const referenceFailed = categories.isError || locations.isError
  const current = STEPS[step]
  const Illustration = current.illustration

  if (referenceLoading) {
    return (
      <section className="mx-auto flex w-full max-w-5xl flex-col gap-6">
        <Skeleton className="h-7 w-72" />
        <DashboardPanel className="flex flex-col gap-4">
          {Array.from({ length: 4 }).map((_, index) => (
            <div key={index} className="flex flex-col gap-2">
              <Skeleton className="h-3.5 w-24" />
              <Skeleton className="h-9 w-full" />
            </div>
          ))}
        </DashboardPanel>
      </section>
    )
  }

  if (referenceFailed) {
    return (
      <DashboardPanel
        role="alert"
        className="mx-auto flex w-full max-w-5xl flex-col items-start gap-3 border-destructive/40 from-destructive/8 via-destructive/5 to-transparent dark:from-destructive/15 dark:via-destructive/8"
      >
        <div>
          <p className="font-heading text-base font-medium">Could not load the form</p>
          <p className="pt-1 text-sm text-muted-foreground">
            The categories and locations this form needs are unavailable. Check the API is
            running, then try again.
          </p>
        </div>
        <Button
          variant="outline"
          onClick={() => {
            categories.refetch()
            locations.refetch()
          }}
        >
          <RotateCwIcon aria-hidden="true" />
          Try again
        </Button>
      </DashboardPanel>
    )
  }

  return (
    <section className="mx-auto flex w-full max-w-5xl flex-col gap-8">
      <DashboardPanel className="py-5">
        <WizardSteps steps={STEPS} current={step} />
      </DashboardPanel>

      <div className="grid gap-8 lg:grid-cols-[1.5fr_1fr]">
        <DashboardPanel
          key={step}
          className="fu-step-in"
          style={{ '--fu-step-from': direction === 'forward' ? '14px' : '-14px' } as React.CSSProperties}
        >
            {step === 0 && (
              <div className="flex flex-col gap-5">
                <Field label="Category" htmlFor="category" errors={fieldErrors.CategoryId}>
                  <FormSelect
                    id="category"
                    value={categoryId}
                    onValueChange={(next) => {
                      setCategoryId(next)
                      setItemTypeId('') // the old item type belongs to a different category
                    }}
                    options={(categories.data ?? []).map((c) => ({ value: c.id, label: c.name }))}
                    placeholder="Choose a category"
                    invalid={Boolean(fieldErrors.CategoryId)}
                  />
                </Field>

                <Field label="Item type" htmlFor="itemType" errors={fieldErrors.ItemTypeId}>
                  <FormSelect
                    id="itemType"
                    value={itemTypeId}
                    onValueChange={setItemTypeId}
                    options={itemTypes.map((t) => ({ value: t.id, label: t.name }))}
                    placeholder={categoryId ? 'Choose an item type' : 'Pick a category first'}
                    disabled={!categoryId || itemTypes.length === 0}
                    invalid={Boolean(fieldErrors.ItemTypeId)}
                  />
                  {categoryId && itemTypes.length === 0 && (
                    <p className="text-xs text-muted-foreground">
                      This category has no item types yet - pick another.
                    </p>
                  )}
                </Field>
              </div>
            )}

            {step === 1 && (
              <div className="flex flex-col gap-5">
                <Field
                  label="Where did you last have it?"
                  htmlFor="location"
                  errors={fieldErrors.LastSeenLocationId}
                >
                  <FormSelect
                    id="location"
                    value={locationId}
                    onValueChange={setLocationId}
                    options={(locations.data ?? []).map((l) => ({
                      value: l.id,
                      label: l.building ? `${l.name} - ${l.building}` : l.name,
                    }))}
                    placeholder="Choose a place on campus"
                    invalid={Boolean(fieldErrors.LastSeenLocationId)}
                  />
                </Field>

                <div className="flex flex-col gap-5">
                  <Field label="Lost between" htmlFor="from" errors={fieldErrors.EstimatedLostFromAt}>
                    <DateTimePicker
                      id="from"
                      value={from}
                      onChange={setFrom}
                      invalid={Boolean(fieldErrors.EstimatedLostFromAt)}
                    />
                  </Field>

                  <Field label="and" htmlFor="to" errors={fieldErrors.EstimatedLostToAt}>
                    <DateTimePicker
                      id="to"
                      value={to}
                      onChange={setTo}
                      invalid={Boolean(fieldErrors.EstimatedLostToAt)}
                    />
                  </Field>
                </div>

                {from && to && new Date(from) > new Date(to) && (
                  <p className="text-sm text-destructive">
                    The end of the window must be after the start.
                  </p>
                )}
              </div>
            )}

            {step === 2 && (
              <div className="flex flex-col gap-5">
                <Field label="Description" htmlFor="description" errors={fieldErrors.Description}>
                  <Textarea
                    id="description"
                    rows={5}
                    value={description}
                    onChange={(event) => setDescription(event.target.value)}
                    placeholder="Navy backpack with a laptop sleeve inside and a broken zip on the front pocket."
                    aria-invalid={Boolean(fieldErrors.Description)}
                  />
                  <p className="text-xs text-muted-foreground">
                    {description.trim().length < 10
                      ? `${10 - description.trim().length} more characters needed`
                      : `${description.trim().length} characters`}
                  </p>
                </Field>

                <Field
                  label="Main colour"
                  htmlFor="primaryColor"
                  optional
                  errors={fieldErrors.PrimaryColor}
                >
                  <Input
                    id="primaryColor"
                    value={primaryColor}
                    onChange={(event) => setPrimaryColor(event.target.value)}
                    placeholder="Navy"
                  />
                </Field>

                <PhotoPicker files={photos} onChange={setPhotos} />
              </div>
            )}

            {step === 3 && (
              <dl className="flex flex-col divide-y divide-border">
                <Summary label="Item" value={`${category?.name ?? '-'} · ${itemType?.name ?? '-'}`} />
                <Summary label="Last seen" value={location?.name ?? '-'} />
                <Summary
                  label="Lost between"
                  value={`${new Date(from).toLocaleString('en', { day: 'numeric', month: 'short', hour: 'numeric', minute: '2-digit' })} - ${new Date(to).toLocaleString('en', { hour: 'numeric', minute: '2-digit' })}`}
                />
                {primaryColor.trim() && <Summary label="Colour" value={primaryColor.trim()} />}
                {photos.length > 0 && (
                  <Summary
                    label="Photos"
                    value={`${photos.length} image${photos.length === 1 ? '' : 's'} attached`}
                  />
                )}
                <Summary label="Description" value={description.trim()} />
              </dl>
            )}
        </DashboardPanel>

        {/* Context panel: what this step is for, and why it matters. */}
        <DashboardPanel
          key={`aside-${step}`}
          aria-label="About this step"
          className="fu-step-in flex h-fit flex-col gap-4"
        >
          <div>
            <h1 className="text-2xl font-semibold tracking-tight text-balance">{current.title}</h1>
            <p className="pt-2 text-sm text-pretty text-muted-foreground">{current.body}</p>
          </div>

          <Illustration className="mx-auto w-full max-w-56 text-primary/70" />
        </DashboardPanel>
      </div>

      {/* -------------------------------------------------------------- footer */}
      <div className="flex items-center justify-between gap-3 border-t pt-5">
        {step === 0 ? (
          <Button variant="ghost" nativeButton={false} render={<Link to="/my-reports" />}>
            Cancel
          </Button>
        ) : (
          <Button variant="ghost" onClick={() => go(step - 1)} disabled={mutation.isPending}>
            <ArrowLeftIcon aria-hidden="true" />
            Back
          </Button>
        )}

        {step < STEPS.length - 1 ? (
          <Button onClick={() => go(step + 1)} disabled={!stepIsValid}>
            Continue
            <ArrowRightIcon aria-hidden="true" />
          </Button>
        ) : (
          <Button onClick={handleSubmit} disabled={mutation.isPending}>
            {mutation.isPending ? (
              <Loader2Icon className="animate-spin" aria-hidden="true" />
            ) : (
              <CheckIcon aria-hidden="true" />
            )}
            {mutation.isPending ? 'Posting...' : 'Post report'}
          </Button>
        )}
      </div>
    </section>
  )
}

function Field({
  label,
  htmlFor,
  optional,
  errors,
  children,
}: {
  label: string
  htmlFor: string
  optional?: boolean
  errors?: string[]
  children: React.ReactNode
}) {
  return (
    <div className="flex flex-col gap-2">
      <Label htmlFor={htmlFor}>
        {label}
        {optional && <span className="pl-1.5 font-normal text-muted-foreground">(optional)</span>}
      </Label>
      {children}
      {errors && (
        <p id={`${htmlFor}-error`} className="text-sm text-destructive">
          {errors.join(' ')}
        </p>
      )}
    </div>
  )
}

function Summary({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex flex-col gap-1 py-3 sm:flex-row sm:gap-4">
      <dt className="text-sm text-muted-foreground sm:w-36 sm:shrink-0">{label}</dt>
      <dd className="text-sm text-pretty">{value}</dd>
    </div>
  )
}

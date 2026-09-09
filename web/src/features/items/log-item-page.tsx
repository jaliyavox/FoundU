import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeftIcon, EyeOffIcon, Loader2Icon, RotateCwIcon } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { DashboardPanel, PanelDivider } from '@/components/layout/dashboard-panel'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { Textarea } from '@/components/ui/textarea'
import { DateTimePicker } from '@/features/reports/date-time-picker'
import { FormSelect } from '@/features/reports/form-select'
import {
  defaultWindow,
  getCategories,
  getLocations,
  getStorageLocations,
  toUtcIso,
} from '@/features/reports/reports-api'
import { ApiError } from '@/lib/api/client'
import { createItem } from './items-api'

/**
 * Logging something handed in at the desk.
 *
 * One screen rather than a wizard: the student's report is filled in on a phone after losing
 * something, but this is typed at a counter with the item in hand and someone waiting.
 */
export function LogItemPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const categories = useQuery({ queryKey: ['categories'], queryFn: getCategories })
  const locations = useQuery({ queryKey: ['locations'], queryFn: getLocations })
  const storage = useQuery({ queryKey: ['storage-locations'], queryFn: getStorageLocations })

  const [categoryId, setCategoryId] = useState('')
  const [itemTypeId, setItemTypeId] = useState('')
  const [foundLocationId, setFoundLocationId] = useState('')
  const [storageLocationId, setStorageLocationId] = useState('')
  const [generalDescription, setGeneralDescription] = useState('')
  const [privateVerificationDetails, setPrivateVerificationDetails] = useState('')
  const [primaryColor, setPrimaryColor] = useState('')
  const [foundAt, setFoundAt] = useState(() => defaultWindow().to)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({})

  const create = useMutation({
    mutationFn: () =>
      createItem({
        categoryId,
        itemTypeId,
        foundLocationId,
        storageLocationId,
        generalDescription: generalDescription.trim(),
        privateVerificationDetails: privateVerificationDetails.trim() || undefined,
        primaryColor: primaryColor.trim() || undefined,
        foundAt: toUtcIso(foundAt),
      }),
    onSuccess: (item) => {
      queryClient.invalidateQueries({ queryKey: ['found-items'] })
      toast.success('Logged. It is now searchable at the desk.')
      navigate(`/items/${item.id}`)
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

  const referenceLoading = categories.isPending || locations.isPending || storage.isPending
  const referenceFailed = categories.isError || locations.isError || storage.isError

  if (referenceLoading) {
    return (
      <section className="mx-auto flex w-full max-w-2xl flex-col gap-6">
        <Skeleton className="h-7 w-56" />
        <DashboardPanel className="flex flex-col gap-4">
          {Array.from({ length: 5 }).map((_, index) => (
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
        className="mx-auto flex w-full max-w-2xl flex-col items-start gap-3 border-destructive/40 from-destructive/8 via-destructive/5 to-transparent dark:from-destructive/15 dark:via-destructive/8"
      >
        <div>
          <p className="font-heading text-base font-medium">Could not load the form</p>
          <p className="pt-1 text-sm text-muted-foreground">
            The categories, locations and storage places this form needs are unavailable.
          </p>
        </div>
        <Button
          variant="outline"
          onClick={() => {
            categories.refetch()
            locations.refetch()
            storage.refetch()
          }}
        >
          <RotateCwIcon aria-hidden="true" />
          Try again
        </Button>
      </DashboardPanel>
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
      <Button
        variant="ghost"
        size="sm"
        className="self-start text-muted-foreground"
        nativeButton={false}
        render={<Link to="/items" />}
      >
        <ArrowLeftIcon aria-hidden="true" />
        Back to items
      </Button>

      <DashboardPanel>
        <h1 className="text-2xl font-semibold tracking-tight">Log an item</h1>
        <p className="pt-1 text-sm text-muted-foreground">
          Something handed in at the desk. It becomes searchable straight away.
        </p>
      </DashboardPanel>

      <form onSubmit={handleSubmit} className="flex flex-col gap-6">
        <DashboardPanel className="flex flex-col gap-5">
          <div className="grid gap-5 sm:grid-cols-2">
            <Field label="Category" htmlFor="category" errors={fieldErrors.CategoryId}>
              <FormSelect
                id="category"
                value={categoryId}
                onValueChange={(next) => {
                  setCategoryId(next)
                  setItemTypeId('') // the old type belongs to a different category
                }}
                options={categories.data.map((c) => ({ value: c.id, label: c.name }))}
                placeholder="Choose a category"
                invalid={Boolean(fieldErrors.CategoryId)}
              />
            </Field>

            <Field label="Item type" htmlFor="item-type" errors={fieldErrors.ItemTypeId}>
              <FormSelect
                id="item-type"
                value={itemTypeId}
                onValueChange={setItemTypeId}
                options={itemTypes.map((t) => ({ value: t.id, label: t.name }))}
                placeholder={categoryId ? 'Choose an item type' : 'Pick a category first'}
                disabled={!categoryId}
                invalid={Boolean(fieldErrors.ItemTypeId)}
              />
            </Field>

            <Field label="Found at" htmlFor="found-location" errors={fieldErrors.FoundLocationId}>
              <FormSelect
                id="found-location"
                value={foundLocationId}
                onValueChange={setFoundLocationId}
                options={locations.data.map((l) => ({ value: l.id, label: l.name }))}
                placeholder="Where it was found"
                invalid={Boolean(fieldErrors.FoundLocationId)}
              />
            </Field>

            <Field label="Kept at" htmlFor="storage" errors={fieldErrors.StorageLocationId}>
              <FormSelect
                id="storage"
                value={storageLocationId}
                onValueChange={setStorageLocationId}
                options={storage.data.map((s) => ({ value: s.id, label: s.name }))}
                placeholder="Where it is stored"
                invalid={Boolean(fieldErrors.StorageLocationId)}
              />
            </Field>

            <Field label="When it was found" htmlFor="found-at" errors={fieldErrors.FoundAt}>
              <DateTimePicker
                id="found-at"
                value={foundAt}
                onChange={setFoundAt}
                invalid={Boolean(fieldErrors.FoundAt)}
              />
            </Field>

            <Field label="Main colour" htmlFor="primary-color" errors={fieldErrors.PrimaryColor}>
              <Input
                id="primary-color"
                value={primaryColor}
                onChange={(event) => setPrimaryColor(event.target.value)}
                placeholder="Navy"
              />
            </Field>
          </div>

          <PanelDivider />

          <Field
            label="Description"
            htmlFor="description"
            errors={fieldErrors.GeneralDescription}
            hint="What anyone can see by looking at it. Used for matching."
          >
            <Textarea
              id="description"
              rows={3}
              required
              value={generalDescription}
              onChange={(event) => setGeneralDescription(event.target.value)}
              placeholder="Navy backpack with a broken zip pull, handed in at the library desk."
              aria-invalid={Boolean(fieldErrors.GeneralDescription)}
            />
          </Field>
        </DashboardPanel>

        {/* The whole verification model rests on this field, so it gets its own panel and
            says plainly what it is for. */}
        <DashboardPanel className="flex flex-col gap-4 border-amber-500/30 from-amber-500/8 via-amber-500/4 to-transparent dark:from-amber-500/12">
          <div className="flex items-center gap-2">
            <EyeOffIcon className="size-4 text-amber-700 dark:text-amber-300" aria-hidden="true" />
            <h2 className="font-heading text-base font-medium">Hidden verification detail</h2>
          </div>

          <Field
            label="Something only the owner would know"
            htmlFor="private"
            errors={fieldErrors.PrivateVerificationDetails}
            hint="Never shown to a claimant. Claim questions are written from it, and answering from memory is what proves the item is theirs. Leave it blank if there is genuinely nothing distinctive - do not invent one."
          >
            <Textarea
              id="private"
              rows={2}
              value={privateVerificationDetails}
              onChange={(event) => setPrivateVerificationDetails(event.target.value)}
              placeholder="Pink keychain in the front pocket, and a bus ticket dated the 18th."
              aria-invalid={Boolean(fieldErrors.PrivateVerificationDetails)}
            />
          </Field>
        </DashboardPanel>

        <div className="flex justify-end gap-3">
          <Button variant="ghost" nativeButton={false} render={<Link to="/items" />}>
            Cancel
          </Button>
          <Button
            type="submit"
            disabled={create.isPending}
            className="bg-brand-forest text-white hover:bg-brand-forest/90"
          >
            {create.isPending && <Loader2Icon className="animate-spin" aria-hidden="true" />}
            Log the item
          </Button>
        </div>
      </form>
    </section>
  )
}

function Field({
  label,
  htmlFor,
  errors,
  hint,
  children,
}: {
  label: string
  htmlFor: string
  errors?: string[]
  hint?: string
  children: React.ReactNode
}) {
  return (
    <div className="flex flex-col gap-2">
      <Label htmlFor={htmlFor}>{label}</Label>
      {children}
      {hint && !errors && <p className="text-xs text-pretty text-muted-foreground">{hint}</p>}
      {errors && <p className="text-sm text-destructive">{errors.join(' ')}</p>}
    </div>
  )
}

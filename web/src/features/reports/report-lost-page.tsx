import { useMemo, useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeftIcon, Loader2Icon, RotateCwIcon } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { Textarea } from '@/components/ui/textarea'
import { FormSelect } from './form-select'
import {
  createLostReport,
  defaultWindow,
  getCategories,
  getLocations,
  toUtcIso,
} from './reports-api'
import { ApiError } from '@/lib/api/client'

export function ReportLostPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const initialWindow = useMemo(defaultWindow, [])

  const categories = useQuery({ queryKey: ['reference', 'categories'], queryFn: getCategories })
  const locations = useQuery({ queryKey: ['reference', 'locations'], queryFn: getLocations })

  const [categoryId, setCategoryId] = useState('')
  const [itemTypeId, setItemTypeId] = useState('')
  const [locationId, setLocationId] = useState('')
  const [description, setDescription] = useState('')
  const [primaryColor, setPrimaryColor] = useState('')
  const [from, setFrom] = useState(initialWindow.from)
  const [to, setTo] = useState(initialWindow.to)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({})

  const itemTypes = categories.data?.find((c) => c.id === categoryId)?.itemTypes ?? []

  const mutation = useMutation({
    mutationFn: createLostReport,
    onSuccess: () => {
      // The new report belongs in both the student's list and the public feed.
      queryClient.invalidateQueries({ queryKey: ['my-lost-reports'] })
      queryClient.invalidateQueries({ queryKey: ['lost-feed'] })
      toast.success('Report submitted. We will tell you if something matching turns up.')
      navigate('/my-reports')
    },
    onError: (error) => {
      if (error instanceof ApiError) {
        setFieldErrors(error.fieldErrors)
        toast.error(error.message)
      } else {
        toast.error('Could not reach the server.')
      }
    },
  })

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
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

  const referenceFailed = categories.isError || locations.isError
  const referenceLoading = categories.isPending || locations.isPending

  return (
    <section className="mx-auto flex w-full max-w-2xl flex-col gap-6">
      <div>
        <Button
          variant="ghost"
          size="sm"
          className="-ml-2 mb-2"
          nativeButton={false}
          render={<a href="/my-reports" />}
        >
          <ArrowLeftIcon aria-hidden="true" />
          My reports
        </Button>
        <h1 className="text-2xl font-semibold tracking-tight">Report a lost item</h1>
        <p className="pt-1 text-sm text-muted-foreground">
          The more detail you give, the better the chance of a match. Your report also appears on
          the public lost feed.
        </p>
      </div>

      {referenceLoading ? (
        <Card>
          <CardContent className="flex flex-col gap-4 pt-6">
            {Array.from({ length: 5 }).map((_, index) => (
              <div key={index} className="flex flex-col gap-2">
                <Skeleton className="h-3.5 w-24" />
                <Skeleton className="h-9 w-full" />
              </div>
            ))}
          </CardContent>
        </Card>
      ) : referenceFailed ? (
        <Card role="alert" className="border-destructive/40 bg-destructive/5">
          <CardHeader>
            <CardTitle className="text-base">Could not load the form</CardTitle>
            <CardDescription>
              The categories and locations this form needs are unavailable. Check the API is
              running, then try again.
            </CardDescription>
          </CardHeader>
          <CardContent>
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
          </CardContent>
        </Card>
      ) : (
        <Card>
          <CardContent className="pt-6">
            <form onSubmit={handleSubmit} noValidate className="flex flex-col gap-5">
              <div className="grid gap-5 sm:grid-cols-2">
                <div className="flex flex-col gap-2">
                  <Label htmlFor="category">Category</Label>
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
                  <FieldError id="category" errors={fieldErrors.CategoryId} />
                </div>

                <div className="flex flex-col gap-2">
                  <Label htmlFor="itemType">Item type</Label>
                  <FormSelect
                    id="itemType"
                    value={itemTypeId}
                    onValueChange={setItemTypeId}
                    options={itemTypes.map((t) => ({ value: t.id, label: t.name }))}
                    placeholder={categoryId ? 'Choose an item type' : 'Pick a category first'}
                    disabled={!categoryId || itemTypes.length === 0}
                    invalid={Boolean(fieldErrors.ItemTypeId)}
                  />
                  <FieldError id="itemType" errors={fieldErrors.ItemTypeId} />
                  {categoryId && itemTypes.length === 0 && (
                    <p className="text-xs text-muted-foreground">
                      This category has no item types yet - pick another.
                    </p>
                  )}
                </div>
              </div>

              <div className="flex flex-col gap-2">
                <Label htmlFor="location">Where did you last have it?</Label>
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
                <FieldError id="location" errors={fieldErrors.LastSeenLocationId} />
              </div>

              <div className="flex flex-col gap-2">
                <Label htmlFor="description">Description</Label>
                <Textarea
                  id="description"
                  rows={4}
                  required
                  value={description}
                  onChange={(event) => setDescription(event.target.value)}
                  placeholder="Navy backpack with a laptop sleeve inside and a broken zip on the front pocket."
                  aria-invalid={Boolean(fieldErrors.Description)}
                  aria-describedby={fieldErrors.Description ? 'description-error' : undefined}
                />
                <FieldError id="description" errors={fieldErrors.Description} />
              </div>

              <div className="grid gap-5 sm:grid-cols-3">
                <div className="flex flex-col gap-2">
                  <Label htmlFor="primaryColor">
                    Main colour <span className="font-normal text-muted-foreground">(optional)</span>
                  </Label>
                  <Input
                    id="primaryColor"
                    value={primaryColor}
                    onChange={(event) => setPrimaryColor(event.target.value)}
                    placeholder="Navy"
                    aria-invalid={Boolean(fieldErrors.PrimaryColor)}
                  />
                  <FieldError id="primaryColor" errors={fieldErrors.PrimaryColor} />
                </div>

                <div className="flex flex-col gap-2">
                  <Label htmlFor="from">Lost between</Label>
                  <Input
                    id="from"
                    type="datetime-local"
                    required
                    value={from}
                    onChange={(event) => setFrom(event.target.value)}
                    aria-invalid={Boolean(fieldErrors.EstimatedLostFromAt)}
                  />
                  <FieldError id="from" errors={fieldErrors.EstimatedLostFromAt} />
                </div>

                <div className="flex flex-col gap-2">
                  <Label htmlFor="to">and</Label>
                  <Input
                    id="to"
                    type="datetime-local"
                    required
                    value={to}
                    onChange={(event) => setTo(event.target.value)}
                    aria-invalid={Boolean(fieldErrors.EstimatedLostToAt)}
                  />
                  <FieldError id="to" errors={fieldErrors.EstimatedLostToAt} />
                </div>
              </div>

              <div className="flex gap-3 pt-1">
                <Button type="submit" disabled={mutation.isPending}>
                  {mutation.isPending && <Loader2Icon className="animate-spin" aria-hidden="true" />}
                  {mutation.isPending ? 'Submitting...' : 'Submit report'}
                </Button>
                <Button
                  type="button"
                  variant="ghost"
                  onClick={() => navigate('/my-reports')}
                  disabled={mutation.isPending}
                >
                  Cancel
                </Button>
              </div>
            </form>
          </CardContent>
        </Card>
      )}
    </section>
  )
}

function FieldError({ id, errors }: { id: string; errors?: string[] }) {
  if (!errors) return null
  return (
    <p id={`${id}-error`} className="text-sm text-destructive">
      {errors.join(' ')}
    </p>
  )
}

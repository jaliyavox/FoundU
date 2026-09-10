import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Loader2Icon, PencilIcon } from 'lucide-react'
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
import { FormSelect } from './form-select'
import { DateTimePicker } from './date-time-picker'
import {
  getCategories,
  getLocations,
  updateLostReport,
  type LostReportListItem,
  type LostReportDetail,
} from './reports-api'
import { ApiError } from '@/lib/api/client'

interface EditLostReportDialogProps {
  report: LostReportListItem | LostReportDetail | null
  open: boolean
  onClose: () => void
}

export function EditLostReportDialog({ report, open, onClose }: EditLostReportDialogProps) {
  const queryClient = useQueryClient()

  const { data: categories = [] } = useQuery({
    queryKey: ['categories'],
    queryFn: getCategories,
  })

  const { data: locations = [] } = useQuery({
    queryKey: ['locations'],
    queryFn: getLocations,
  })

  const [categoryId, setCategoryId] = useState('')
  const [itemTypeId, setItemTypeId] = useState('')
  const [lastSeenLocationId, setLastSeenLocationId] = useState('')
  const [description, setDescription] = useState('')
  const [primaryColor, setPrimaryColor] = useState('')
  const [secondaryColor, setSecondaryColor] = useState('')
  const [fromTime, setFromTime] = useState('')
  const [toTime, setToTime] = useState('')

  const selectedCategory = categories.find((c) => c.id === categoryId)

  useEffect(() => {
    if (report && open) {
      setDescription(report.description || '')
      setPrimaryColor(report.primaryColor || '')
      setFromTime(report.estimatedLostFromAt ? new Date(report.estimatedLostFromAt).toISOString().slice(0, 16) : '')
      setToTime(report.estimatedLostToAt ? new Date(report.estimatedLostToAt).toISOString().slice(0, 16) : '')

      if ('categoryId' in report && report.categoryId) {
        setCategoryId(report.categoryId)
        setItemTypeId(report.itemTypeId)
        setLastSeenLocationId(report.lastSeenLocationId)
        if ('secondaryColor' in report) {
          setSecondaryColor(report.secondaryColor || '')
        }
      } else if (categories.length > 0) {
        const cat = categories.find((c) => c.name === report.categoryName)
        if (cat) {
          setCategoryId(cat.id)
          const item = cat.itemTypes.find((it) => it.name === report.itemTypeName)
          if (item) setItemTypeId(item.id)
        }
        const loc = locations.find((l) => l.name === report.lastSeenLocationName)
        if (loc) setLastSeenLocationId(loc.id)
      }
    }
  }, [report, open, categories, locations])

  const updateMutation = useMutation({
    mutationFn: async () => {
      if (!report) return
      return updateLostReport(report.id, {
        categoryId,
        itemTypeId,
        lastSeenLocationId,
        description: description.trim(),
        primaryColor: primaryColor.trim() || undefined,
        secondaryColor: secondaryColor.trim() || undefined,
        estimatedLostFromAt: new Date(fromTime).toISOString(),
        estimatedLostToAt: new Date(toTime).toISOString(),
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['my-lost-reports'] })
      queryClient.invalidateQueries({ queryKey: ['lost-report', report?.id] })
      toast.success('Report updated successfully.')
      onClose()
    },
    onError: (err) => {
      toast.error(err instanceof ApiError ? err.message : 'Failed to update report.')
    },
  })

  return (
    <Dialog open={open} onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <PencilIcon className="size-5 text-primary" />
            Edit Active Lost Report
          </DialogTitle>
          <DialogDescription>
            Update the description, category, or last-seen details of your report.
          </DialogDescription>
        </DialogHeader>

        <form
          onSubmit={(e) => {
            e.preventDefault()
            updateMutation.mutate()
          }}
          className="space-y-4 py-2"
        >
          <div className="grid gap-2">
            <Label htmlFor="category">Category</Label>
            <FormSelect
              id="category"
              value={categoryId}
              onValueChange={(val: string) => {
                setCategoryId(val)
                setItemTypeId('')
              }}
              options={categories.map((c) => ({ value: c.id, label: c.name }))}
              placeholder="Select category"
            />
          </div>

          <div className="grid gap-2">
            <Label htmlFor="itemType">Item Type</Label>
            <FormSelect
              id="itemType"
              value={itemTypeId}
              onValueChange={setItemTypeId}
              options={(selectedCategory?.itemTypes || []).map((i) => ({ value: i.id, label: i.name }))}
              placeholder="Select item type"
              disabled={!categoryId}
            />
          </div>

          <div className="grid gap-2">
            <Label htmlFor="location">Last-Seen Location</Label>
            <FormSelect
              id="location"
              value={lastSeenLocationId}
              onValueChange={setLastSeenLocationId}
              options={locations.map((l) => ({ value: l.id, label: l.name }))}
              placeholder="Select location"
            />
          </div>

          <div className="grid gap-2">
            <Label htmlFor="description">Description</Label>
            <Textarea
              id="description"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              rows={3}
              placeholder="Provide identifying features..."
              required
            />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <Label htmlFor="primaryColor">Primary Color</Label>
              <Input
                id="primaryColor"
                value={primaryColor}
                onChange={(e) => setPrimaryColor(e.target.value)}
                placeholder="e.g. Black"
              />
            </div>
            <div>
              <Label htmlFor="secondaryColor">Secondary Color</Label>
              <Input
                id="secondaryColor"
                value={secondaryColor}
                onChange={(e) => setSecondaryColor(e.target.value)}
                placeholder="e.g. Silver"
              />
            </div>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <Label>Lost From</Label>
              <DateTimePicker id="fromTime" value={fromTime} onChange={setFromTime} />
            </div>
            <div>
              <Label>Lost To</Label>
              <DateTimePicker id="toTime" value={toTime} onChange={setToTime} />
            </div>
          </div>

          <DialogFooter className="pt-3">
            <Button type="button" variant="outline" onClick={onClose} disabled={updateMutation.isPending}>
              Cancel
            </Button>
            <Button type="submit" disabled={updateMutation.isPending || !categoryId || !itemTypeId || !lastSeenLocationId}>
              {updateMutation.isPending && <Loader2Icon className="mr-2 size-4 animate-spin" />}
              Save changes
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

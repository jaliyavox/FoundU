import { useState, type FormEvent, type ReactNode } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  ArchiveRestoreIcon,
  ArchiveXIcon,
  CheckIcon,
  ChevronDownIcon,
  Loader2Icon,
  PencilIcon,
  PlusIcon,
  Trash2Icon,
  XIcon,
} from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { DashboardPanel, PanelDivider } from '@/components/layout/dashboard-panel'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import { ApiError } from '@/lib/api/client'
import { cn } from '@/lib/utils'
import {
  createReference,
  deleteReference,
  getReferenceAdmin,
  setReferenceActive,
  updateReference,
  type CategoryAdmin,
  type ReferenceItemInput,
  type ReferenceKind,
} from './reference-api'

type Tab = 'categories' | 'locations' | 'storage'

const TABS: { value: Tab; label: string }[] = [
  { value: 'categories', label: 'Categories & item types' },
  { value: 'locations', label: 'Campus places' },
  { value: 'storage', label: 'Storage' },
]

/**
 * The lists everyone picks from - what a student can say they lost, where they lost it, and
 * where the desk keeps things - for admins to look after.
 *
 * Anything a report already names can be retired: it leaves the pickers and every record
 * that names it keeps reading correctly. Only something nothing uses can be deleted, and the
 * delete button says why when it is off.
 */
export function ReferencePage() {
  const [tab, setTab] = useState<Tab>('categories')
  const { data, isPending, isError, refetch } = useQuery({
    queryKey: ['admin-reference'],
    queryFn: getReferenceAdmin,
  })

  return (
    <section className="flex w-full flex-col gap-6">
      <div>
        <p className="text-sm font-medium text-brand-green">Admin</p>
        <h1 className="pt-1 text-2xl font-semibold tracking-tight">Places & categories</h1>
        <p className="max-w-2xl pt-2 text-sm text-muted-foreground">
          What students can say they lost, where they lost it, and where the desk keeps things.
          Retire anything in use - it leaves the pickers and old records still read correctly.
          Delete is only for what nothing uses.
        </p>
      </div>

      <div role="tablist" aria-label="Which list" className="inline-flex w-fit flex-wrap gap-1 rounded-xl border border-foreground/10 p-1">
        {TABS.map(item => (
          <button
            key={item.value}
            type="button"
            role="tab"
            aria-selected={tab === item.value}
            onClick={() => setTab(item.value)}
            className={cn(
              'rounded-lg px-3.5 py-1.5 text-sm font-medium transition-colors focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none',
              tab === item.value ? 'bg-foreground text-background' : 'text-muted-foreground hover:text-foreground',
            )}
          >
            {item.label}
          </button>
        ))}
      </div>

      {isPending ? (
        <Skeleton className="h-72 w-full" />
      ) : isError ? (
        <DashboardPanel className="flex flex-col items-start gap-3" role="alert">
          <p className="text-sm">Could not load the lists.</p>
          <Button variant="outline" size="sm" onClick={() => refetch()}>Try again</Button>
        </DashboardPanel>
      ) : tab === 'categories' ? (
        <CategoriesList categories={data.categories} />
      ) : tab === 'locations' ? (
        <SimpleList
          kind="locations"
          title="Campus places"
          hint="Halls, labs, the library - wherever a student might say they lost something."
          rows={data.locations.map(l => ({ ...l, detail: [l.building, l.description].filter(Boolean).join(' · ') }))}
          extraFields={['building', 'description']}
        />
      ) : (
        <SimpleList
          kind="storage"
          title="Storage"
          hint="Where the desk keeps what is handed in. At least one has to stay open."
          rows={data.storage.map(s => ({
            ...s,
            detail: [s.building, s.capacity != null ? `holds ${s.capacity}` : null].filter(Boolean).join(' · '),
          }))}
          extraFields={['building', 'capacity']}
        />
      )}
    </section>
  )
}

/* ------------------------------------------------------------------ mutations */

function useReferenceActions() {
  const queryClient = useQueryClient()
  const done = (message: string) => {
    // The pickers everyone else uses read these lists too.
    queryClient.invalidateQueries({ queryKey: ['admin-reference'] })
    queryClient.invalidateQueries({ queryKey: ['categories'] })
    queryClient.invalidateQueries({ queryKey: ['locations'] })
    queryClient.invalidateQueries({ queryKey: ['storage-locations'] })
    toast.success(message)
  }
  const failed = (error: unknown) => toast.error(error instanceof ApiError ? error.message : 'Could not save that.')

  return {
    create: useMutation({
      mutationFn: ({ kind, input }: { kind: ReferenceKind; input: ReferenceItemInput }) => createReference(kind, input),
      onSuccess: () => done('Added.'),
      onError: failed,
    }),
    update: useMutation({
      mutationFn: ({ kind, id, input }: { kind: ReferenceKind; id: string; input: ReferenceItemInput }) => updateReference(kind, id, input),
      onSuccess: () => done('Saved.'),
      onError: failed,
    }),
    setActive: useMutation({
      mutationFn: ({ kind, id, isActive }: { kind: ReferenceKind; id: string; isActive: boolean }) => setReferenceActive(kind, id, isActive),
      onSuccess: (_, { isActive }) => done(isActive ? 'Restored - it is back in the pickers.' : 'Retired - it is out of the pickers.'),
      onError: failed,
    }),
    remove: useMutation({
      mutationFn: ({ kind, id }: { kind: ReferenceKind; id: string }) => deleteReference(kind, id),
      onSuccess: () => done('Deleted.'),
      onError: failed,
    }),
  }
}

/* ------------------------------------------------------------------ rows */

interface RowData {
  id: string
  name: string
  isActive: boolean
  usageCount: number
  detail?: string
}

/**
 * One row: its name, how much it is used, and what can be done with it. Renaming happens in
 * place; delete asks once and explains itself when it is not allowed.
 */
function ReferenceRow({
  kind,
  row,
  editInput,
  children,
}: {
  kind: ReferenceKind
  row: RowData
  /** What to send when renaming - the other fields kept as they were. */
  editInput: (name: string) => ReferenceItemInput
  children?: ReactNode
}) {
  const actions = useReferenceActions()
  const [editing, setEditing] = useState(false)
  const [name, setName] = useState(row.name)
  const [confirming, setConfirming] = useState(false)
  const busy = actions.update.isPending || actions.setActive.isPending || actions.remove.isPending
  const inUse = row.usageCount > 0

  function save(event: FormEvent) {
    event.preventDefault()
    if (!name.trim()) return
    actions.update.mutate(
      { kind, id: row.id, input: editInput(name.trim()) },
      { onSuccess: () => setEditing(false) },
    )
  }

  return (
    <li className={cn('flex flex-col gap-2 py-3', !row.isActive && 'opacity-60')}>
      <div className="flex flex-wrap items-center gap-x-3 gap-y-2">
        {editing ? (
          <form onSubmit={save} className="flex flex-1 items-center gap-2">
            <Input
              value={name}
              onChange={event => setName(event.target.value)}
              aria-label={`New name for ${row.name}`}
              maxLength={100}
              className="h-8 max-w-xs"
              autoFocus
            />
            <Button type="submit" size="icon-sm" aria-label="Save the name" disabled={busy || !name.trim()}>
              <CheckIcon aria-hidden="true" />
            </Button>
            <Button type="button" variant="ghost" size="icon-sm" aria-label="Cancel" onClick={() => { setEditing(false); setName(row.name) }}>
              <XIcon aria-hidden="true" />
            </Button>
          </form>
        ) : (
          <div className="min-w-0 flex-1">
            <p className="truncate text-sm font-medium">
              {row.name}
              {!row.isActive && <span className="pl-2 text-xs font-normal text-muted-foreground">retired</span>}
            </p>
            <p className="truncate text-xs text-muted-foreground">
              {[row.detail, inUse ? `${row.usageCount} record${row.usageCount === 1 ? '' : 's'}` : 'not used yet']
                .filter(Boolean)
                .join(' · ')}
            </p>
          </div>
        )}

        {!editing && (
          <div className="flex items-center gap-1">
            <Button variant="ghost" size="sm" onClick={() => setEditing(true)} disabled={busy}>
              <PencilIcon aria-hidden="true" />
              Rename
            </Button>

            <Button
              variant="ghost"
              size="sm"
              disabled={busy}
              onClick={() => actions.setActive.mutate({ kind, id: row.id, isActive: !row.isActive })}
            >
              {row.isActive ? <ArchiveXIcon aria-hidden="true" /> : <ArchiveRestoreIcon aria-hidden="true" />}
              {row.isActive ? 'Retire' : 'Restore'}
            </Button>

            {confirming ? (
              <span className="flex items-center gap-1">
                <Button
                  variant="destructive"
                  size="sm"
                  disabled={busy}
                  onClick={() => actions.remove.mutate({ kind, id: row.id }, { onSettled: () => setConfirming(false) })}
                >
                  {actions.remove.isPending && <Loader2Icon className="animate-spin" aria-hidden="true" />}
                  Delete
                </Button>
                <Button variant="ghost" size="sm" onClick={() => setConfirming(false)}>Keep</Button>
              </span>
            ) : (
              <Button
                variant="ghost"
                size="sm"
                disabled={busy || inUse}
                // A disabled button cannot explain itself on hover everywhere, so the reason is
                // in its accessible name and in the tooltip text alike.
                title={inUse ? 'Records name this, so it can only be retired.' : undefined}
                aria-label={inUse ? `Delete ${row.name} - not possible, records name it` : `Delete ${row.name}`}
                onClick={() => setConfirming(true)}
                className="text-destructive hover:text-destructive"
              >
                <Trash2Icon aria-hidden="true" />
                Delete
              </Button>
            )}
          </div>
        )}
      </div>
      {children}
    </li>
  )
}

/* ------------------------------------------------------------------ add */

function AddForm({
  kind,
  label,
  placeholder,
  fields = [],
  categoryId,
  compact,
}: {
  kind: ReferenceKind
  label: string
  placeholder: string
  fields?: ('building' | 'description' | 'capacity')[]
  categoryId?: string
  compact?: boolean
}) {
  const actions = useReferenceActions()
  const [name, setName] = useState('')
  const [building, setBuilding] = useState('')
  const [description, setDescription] = useState('')
  const [capacity, setCapacity] = useState('')

  function submit(event: FormEvent) {
    event.preventDefault()
    if (!name.trim()) return
    actions.create.mutate(
      {
        kind,
        input: {
          name: name.trim(),
          building: building.trim() || null,
          description: description.trim() || null,
          capacity: capacity.trim() ? Number(capacity) : null,
          categoryId: categoryId ?? null,
        },
      },
      {
        onSuccess: () => {
          setName('')
          setBuilding('')
          setDescription('')
          setCapacity('')
        },
      },
    )
  }

  return (
    <form onSubmit={submit} className={cn('flex flex-wrap items-center gap-2', compact && 'pl-4')}>
      <Input
        value={name}
        onChange={event => setName(event.target.value)}
        placeholder={placeholder}
        aria-label={label}
        maxLength={100}
        className="h-9 max-w-xs"
      />
      {fields.includes('building') && (
        <Input value={building} onChange={event => setBuilding(event.target.value)} placeholder="Building (optional)" aria-label="Building" maxLength={150} className="h-9 max-w-[12rem]" />
      )}
      {fields.includes('description') && (
        <Input value={description} onChange={event => setDescription(event.target.value)} placeholder="Note (optional)" aria-label="Note" maxLength={500} className="h-9 max-w-xs" />
      )}
      {fields.includes('capacity') && (
        <Input value={capacity} onChange={event => setCapacity(event.target.value.replace(/\D/g, ''))} placeholder="Capacity" aria-label="Capacity" inputMode="numeric" className="h-9 w-28" />
      )}
      <Button type="submit" size="sm" variant={compact ? 'outline' : 'default'} disabled={actions.create.isPending || !name.trim()}>
        {actions.create.isPending ? <Loader2Icon className="animate-spin" aria-hidden="true" /> : <PlusIcon aria-hidden="true" />}
        {label}
      </Button>
    </form>
  )
}

/* ------------------------------------------------------------------ lists */

function CategoriesList({ categories }: { categories: CategoryAdmin[] }) {
  const [open, setOpen] = useState<string | null>(null)

  return (
    <DashboardPanel className="flex flex-col gap-4">
      <div>
        <h2 className="font-heading text-base font-medium">Categories & item types</h2>
        <p className="text-sm text-muted-foreground">
          What a student picks when they report something. Retiring a category hides its item types with it.
        </p>
      </div>
      <AddForm kind="categories" label="Add category" placeholder="Sports equipment" />
      <PanelDivider />
      <ul className="flex flex-col divide-y divide-foreground/8">
        {categories.map(category => (
          <ReferenceRow
            key={category.id}
            kind="categories"
            row={{
              ...category,
              detail: `${category.itemTypes.length} item type${category.itemTypes.length === 1 ? '' : 's'}`,
            }}
            editInput={name => ({ name, description: category.description, isHighlighted: category.isHighlighted })}
          >
            <button
              type="button"
              onClick={() => setOpen(open === category.id ? null : category.id)}
              aria-expanded={open === category.id}
              className="flex w-fit items-center gap-1 text-xs font-medium text-muted-foreground hover:text-foreground"
            >
              <ChevronDownIcon className={cn('size-3.5 transition-transform', open === category.id && 'rotate-180')} aria-hidden="true" />
              {open === category.id ? 'Hide item types' : 'Item types'}
            </button>
            {open === category.id && (
              <div className="flex flex-col gap-2 border-l border-foreground/10 pl-4">
                <ul className="flex flex-col divide-y divide-foreground/6">
                  {category.itemTypes.map(type => (
                    <ReferenceRow key={type.id} kind="item-types" row={type} editInput={name => ({ name })} />
                  ))}
                </ul>
                <AddForm kind="item-types" label="Add item type" placeholder="Tote bag" categoryId={category.id} compact />
              </div>
            )}
          </ReferenceRow>
        ))}
      </ul>
    </DashboardPanel>
  )
}

function SimpleList({
  kind,
  title,
  hint,
  rows,
  extraFields,
}: {
  kind: 'locations' | 'storage'
  title: string
  hint: string
  rows: (RowData & { building: string | null; description?: string | null; capacity?: number | null })[]
  extraFields: ('building' | 'description' | 'capacity')[]
}) {
  return (
    <DashboardPanel className="flex flex-col gap-4">
      <div>
        <h2 className="font-heading text-base font-medium">{title}</h2>
        <p className="text-sm text-muted-foreground">{hint}</p>
      </div>
      <AddForm
        kind={kind}
        label={kind === 'locations' ? 'Add place' : 'Add storage'}
        placeholder={kind === 'locations' ? 'New Science Hall' : 'Engineering Desk'}
        fields={extraFields}
      />
      <PanelDivider />
      <ul className="flex flex-col divide-y divide-foreground/8">
        {rows.map(row => (
          <ReferenceRow
            key={row.id}
            kind={kind}
            row={row}
            editInput={name => ({
              name,
              building: row.building,
              description: row.description ?? null,
              capacity: row.capacity ?? null,
            })}
          />
        ))}
      </ul>
    </DashboardPanel>
  )
}

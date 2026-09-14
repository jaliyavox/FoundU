import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { RotateCwIcon, TableIcon, TrendingUpIcon } from 'lucide-react'
import {
  Bar,
  BarChart,
  CartesianGrid,
  Legend,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import { Button } from '@/components/ui/button'
import { DashboardPanel, PanelDivider, PanelSheen } from '@/components/layout/dashboard-panel'
import { panelSurface } from '@/components/layout/panel-surface'
import { Skeleton } from '@/components/ui/skeleton'
import { ApiError } from '@/lib/api/client'
import { cn } from '@/lib/utils'
import { getAnalyticsOverview, type DailyActivity, type StatusCount } from './admin-api'

/**
 * Categorical hues, assigned by entity and never cycled. Validated for both themes: every
 * adjacent pair clears the colour-vision and normal-vision floors on the light and the dark
 * surface, so one set serves both. The brand greens on their own do not - they are a ramp,
 * which is what makes them right for the single-series bars below and wrong for three lines.
 */
const SERIES = {
  lostReported: { label: 'Lost reported', color: '#3E9B4A' },
  itemsLoggedIn: { label: 'Items logged in', color: '#4A74D8' },
  itemsReturned: { label: 'Items returned', color: '#C4641F' },
} as const

type SeriesKey = keyof typeof SERIES

const SERIES_ORDER: SeriesKey[] = ['lostReported', 'itemsLoggedIn', 'itemsReturned']

/** Status names as a desk says them, not as the enum spells them. */
const STATUS_LABELS: Record<string, string> = {
  Active: 'Still missing',
  Matched: 'Claim in progress',
  Resolved: 'Returned',
  Withdrawn: 'Withdrawn',
  Unclaimed: 'In storage',
  Claimed: 'Claimed',
  Returned: 'Returned',
  Disposed: 'Disposed',
  Pending: 'Submitted',
  WaitingForAnswer: 'Waiting on claimant',
  UnderReview: 'Under review',
  RevisionRequested: 'Revision requested',
  Approved: 'Approved',
  Rejected: 'Rejected',
  Cancelled: 'Cancelled',
  ManualReviewRequired: 'With a supervisor',
}

const shortDay = (iso: string) =>
  new Date(`${iso}T00:00:00`).toLocaleDateString('en', { day: 'numeric', month: 'short' })

export function AnalyticsPage() {
  const [showTable, setShowTable] = useState(false)

  const { data, isPending, isError, error, refetch } = useQuery({
    queryKey: ['admin-analytics'],
    queryFn: getAnalyticsOverview,
  })

  if (isPending) {
    return (
      <section className="flex flex-col gap-6">
        <DashboardPanel>
          <Skeleton className="h-7 w-48" />
        </DashboardPanel>
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {Array.from({ length: 4 }).map((_, index) => (
            <div key={index} className={cn(panelSurface, 'p-5')}>
              <PanelSheen />
              <Skeleton className="h-3.5 w-24" />
              <Skeleton className="mt-3 h-9 w-16" />
            </div>
          ))}
        </div>
        <DashboardPanel>
          <Skeleton className="h-64 w-full" />
        </DashboardPanel>
      </section>
    )
  }

  if (isError) {
    return (
      <DashboardPanel
        role="alert"
        className="flex flex-col items-start gap-3 border-destructive/40 from-destructive/8 via-destructive/5 to-transparent dark:from-destructive/15 dark:via-destructive/8"
      >
        <div>
          <p className="font-heading text-base font-medium">Could not load the numbers</p>
          <p className="pt-1 text-sm text-muted-foreground">
            {error instanceof ApiError ? error.message : 'Check the API is running.'}
          </p>
        </div>
        <Button variant="outline" onClick={() => refetch()}>
          <RotateCwIcon aria-hidden="true" />
          Try again
        </Button>
      </DashboardPanel>
    )
  }

  const inStorage = data.foundItemsByStatus.find((s) => s.status === 'Unclaimed')?.count ?? 0
  const activity = data.last30Days
  const totals = SERIES_ORDER.map((key) => ({
    key,
    total: activity.reduce((sum, day) => sum + day[key], 0),
  }))
  const quiet = totals.every((t) => t.total === 0)

  return (
    <section className="flex flex-col gap-6">
      <DashboardPanel>
        <h1 className="text-2xl font-semibold tracking-tight">Analytics</h1>
        <p className="pt-1 text-sm text-muted-foreground">
          What the desk has handled. Every figure is a count over real records, nothing is
          estimated.
        </p>
      </DashboardPanel>

      {/* ---------------------------------------------------------- headline */}
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <StatTile label="Items returned to owners" value={data.resolvedReports} featured />
        <StatTile
          label="Mean days to return"
          value={data.averageDaysToReturn === null ? '-' : data.averageDaysToReturn}
          hint={data.averageDaysToReturn === null ? 'No returns yet' : 'From report to approval'}
        />
        <StatTile label="Items in storage" value={inStorage} hint="Handed in, not yet claimed" />
        <StatTile
          label="Flags waiting"
          value={data.openFlags}
          hint={data.openFlags === 0 ? 'Nothing needs a look' : 'On the moderation page'}
        />
      </div>

      {/* ------------------------------------------------------- last 30 days */}
      <DashboardPanel className="flex flex-col gap-4">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <h2 className="font-heading text-base font-medium">The last 30 days</h2>
            <p className="pt-1 text-sm text-muted-foreground">Per day, in UTC. A day with nothing is a quiet day, not missing data.</p>
          </div>
          <Button variant="outline" size="sm" onClick={() => setShowTable((v) => !v)} aria-pressed={showTable}>
            {showTable ? <TrendingUpIcon aria-hidden="true" /> : <TableIcon aria-hidden="true" />}
            {showTable ? 'Show the chart' : 'Show as a table'}
          </Button>
        </div>

        {/* Totals double as the legend, so the identity of each line is carried by a
            swatch and a word, never by colour alone. */}
        <ul className="flex flex-wrap gap-x-6 gap-y-2">
          {totals.map(({ key, total }) => (
            <li key={key} className="flex items-center gap-2 text-sm">
              <span
                aria-hidden="true"
                className="size-2.5 rounded-full"
                style={{ background: SERIES[key].color }}
              />
              <span className="text-muted-foreground">{SERIES[key].label}</span>
              <span className="font-medium tabular-nums">{total}</span>
            </li>
          ))}
        </ul>

        {quiet ? (
          <p className="py-10 text-center text-sm text-muted-foreground">
            Nothing has happened in the last 30 days.
          </p>
        ) : showTable ? (
          <ActivityTable rows={activity} />
        ) : (
          <div className="h-72 w-full">
            <ResponsiveContainer width="100%" height="100%">
              <LineChart data={activity} margin={{ top: 12, right: 56, bottom: 4, left: -16 }}>
                <CartesianGrid stroke="var(--border)" strokeWidth={1} vertical={false} />
                <XAxis
                  dataKey="date"
                  tickFormatter={shortDay}
                  tick={{ fill: 'var(--muted-foreground)', fontSize: 12 }}
                  axisLine={{ stroke: 'var(--border)' }}
                  tickLine={false}
                  minTickGap={28}
                />
                <YAxis
                  allowDecimals={false}
                  tick={{ fill: 'var(--muted-foreground)', fontSize: 12 }}
                  axisLine={false}
                  tickLine={false}
                  width={40}
                />
                <Tooltip content={<ActivityTooltip />} cursor={{ stroke: 'var(--border)', strokeWidth: 1 }} />
                {SERIES_ORDER.map((key) => (
                  <Line
                    key={key}
                    // Straight segments: a smoothed curve dips below zero between a busy day and
                    // a quiet one, which reads as negative reports.
                    type="linear"
                    dataKey={key}
                    name={SERIES[key].label}
                    stroke={SERIES[key].color}
                    strokeWidth={2}
                    strokeLinejoin="round"
                    strokeLinecap="round"
                    dot={false}
                    activeDot={{ r: 5, stroke: 'var(--card)', strokeWidth: 2 }}
                    isAnimationActive={false}
                    // Direct label on the last point only - one number per line, at its end.
                    label={<EndLabel lastIndex={activity.length - 1} color={SERIES[key].color} />}
                  />
                ))}
              </LineChart>
            </ResponsiveContainer>
          </div>
        )}
      </DashboardPanel>

      {/* --------------------------------------------------------- breakdowns */}
      <div className="grid gap-6 lg:grid-cols-3">
        <StatusBreakdown title="Lost reports" rows={data.lostReportsByStatus} order={['Active', 'Matched', 'Resolved', 'Withdrawn']} />
        <StatusBreakdown title="Found items" rows={data.foundItemsByStatus} order={['Unclaimed', 'Claimed', 'Returned', 'Disposed']} />
        <StatusBreakdown
          title="Claims"
          rows={data.claimsByStatus}
          order={['Pending', 'WaitingForAnswer', 'UnderReview', 'RevisionRequested', 'ManualReviewRequired', 'Approved', 'Rejected', 'Cancelled']}
        />
      </div>

      {/* --------------------------------------------------------- categories */}
      <DashboardPanel className="flex flex-col gap-4">
        <div>
          <h2 className="font-heading text-base font-medium">Busiest categories</h2>
          <p className="pt-1 text-sm text-muted-foreground">Reported lost against handed in, all time. Top five.</p>
        </div>

        {data.topCategories.length === 0 ? (
          <p className="py-8 text-center text-sm text-muted-foreground">No reports yet.</p>
        ) : (
          <div style={{ height: 40 + data.topCategories.length * 44 }} className="w-full">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart
                data={data.topCategories}
                layout="vertical"
                barCategoryGap={12}
                barGap={2}
                margin={{ top: 4, right: 32, bottom: 4, left: 8 }}
              >
                <CartesianGrid stroke="var(--border)" strokeWidth={1} horizontal={false} />
                <XAxis
                  type="number"
                  allowDecimals={false}
                  tick={{ fill: 'var(--muted-foreground)', fontSize: 12 }}
                  axisLine={false}
                  tickLine={false}
                />
                <YAxis
                  type="category"
                  dataKey="category"
                  width={132}
                  tick={{ fill: 'var(--foreground)', fontSize: 13 }}
                  axisLine={false}
                  tickLine={false}
                />
                <Tooltip content={<CategoryTooltip />} cursor={{ fill: 'var(--muted)', opacity: 0.4 }} />
                <Legend
                  verticalAlign="top"
                  align="left"
                  iconType="circle"
                  iconSize={8}
                  wrapperStyle={{ fontSize: 13, paddingBottom: 8 }}
                  // The swatch carries identity; the word stays in text colour.
                  formatter={(value: string) => <span style={{ color: 'var(--muted-foreground)' }}>{value}</span>}
                />
                <Bar dataKey="lost" name="Reported lost" fill={SERIES.lostReported.color} barSize={12} radius={[0, 4, 4, 0]} isAnimationActive={false} />
                <Bar dataKey="found" name="Handed in" fill={SERIES.itemsLoggedIn.color} barSize={12} radius={[0, 4, 4, 0]} isAnimationActive={false} />
              </BarChart>
            </ResponsiveContainer>
          </div>
        )}
      </DashboardPanel>

      <p className="text-xs text-muted-foreground">
        Generated {new Date(data.generatedAt).toLocaleString('en', { day: 'numeric', month: 'short', hour: 'numeric', minute: '2-digit' })}.
      </p>
    </section>
  )
}

/* -------------------------------------------------------------------- pieces */

function StatTile({
  label,
  value,
  hint,
  featured,
}: {
  label: string
  value: number | string
  hint?: string
  featured?: boolean
}) {
  return (
    <div
      className={cn(
        panelSurface,
        'p-5',
        featured &&
          'border-brand-forest/60 from-brand-forest via-brand-forest to-[oklch(0.32_0.09_144)] text-white dark:border-brand-forest/60 dark:from-brand-forest dark:via-brand-forest dark:to-[oklch(0.32_0.09_144)]',
      )}
    >
      <PanelSheen className={cn(featured && 'via-white/45')} />
      <div className="relative">
        <p className={cn('text-sm', featured ? 'text-white/70' : 'text-muted-foreground')}>{label}</p>
        {/* Proportional figures on a standalone number - tabular digits belong in columns. */}
        <p className="pt-2 text-3xl font-semibold tracking-tight">{value}</p>
        {hint && (
          <p className={cn('pt-2 text-xs', featured ? 'text-white/60' : 'text-muted-foreground')}>{hint}</p>
        )}
      </div>
    </div>
  )
}

/**
 * Magnitude across a handful of statuses: single hue, one bar per row, the value beside it.
 * Plain HTML - a chart library adds nothing to five bars, and this stays legible in
 * forced-colours mode where SVG fills are stripped.
 */
function StatusBreakdown({ title, rows, order }: { title: string; rows: StatusCount[]; order: string[] }) {
  const byStatus = Object.fromEntries(rows.map((r) => [r.status, r.count]))
  const ordered = order.map((status) => ({ status, count: byStatus[status] ?? 0 })).filter((r) => r.count > 0)
  const max = Math.max(1, ...ordered.map((r) => r.count))
  const total = ordered.reduce((sum, r) => sum + r.count, 0)

  return (
    <DashboardPanel className="flex flex-col gap-4">
      <div className="flex items-baseline justify-between gap-3">
        <h2 className="font-heading text-base font-medium">{title}</h2>
        <span className="text-sm text-muted-foreground tabular-nums">{total} total</span>
      </div>

      <PanelDivider />

      {ordered.length === 0 ? (
        <p className="py-6 text-center text-sm text-muted-foreground">None yet.</p>
      ) : (
        <ul className="flex flex-col gap-3">
          {ordered.map((row) => (
            <li key={row.status} className="grid grid-cols-[1fr_auto] items-center gap-x-3 gap-y-1">
              <span className="text-sm">{STATUS_LABELS[row.status] ?? row.status}</span>
              <span className="text-sm font-medium tabular-nums">{row.count}</span>
              <div className="col-span-2 h-2 overflow-hidden rounded-r-sm bg-foreground/6">
                <div
                  className="h-full rounded-r-sm"
                  style={{ width: `${(row.count / max) * 100}%`, background: SERIES.lostReported.color }}
                />
              </div>
            </li>
          ))}
        </ul>
      )}
    </DashboardPanel>
  )
}

function ActivityTable({ rows }: { rows: DailyActivity[] }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b text-left text-xs tracking-wider text-muted-foreground uppercase">
            <th className="py-2 pr-4 font-medium">Day</th>
            {SERIES_ORDER.map((key) => (
              <th key={key} className="py-2 pr-4 text-right font-medium">{SERIES[key].label}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row.date} className="border-b border-foreground/6">
              <td className="py-1.5 pr-4 text-muted-foreground">{shortDay(row.date)}</td>
              {SERIES_ORDER.map((key) => (
                <td key={key} className="py-1.5 pr-4 text-right tabular-nums">{row[key]}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

/** One number per line, at its end. Anything more is noise nobody reads. */
function EndLabel(props: { x?: number; y?: number; value?: number; index?: number; lastIndex: number; color: string }) {
  const { x, y, value, index, lastIndex } = props
  if (index !== lastIndex || x === undefined || y === undefined) return null
  return (
    <text x={x + 8} y={y} dy={4} fontSize={12} fontWeight={500} fill="var(--foreground)">
      {value}
    </text>
  )
}

interface TooltipPayload {
  name?: string
  value?: number
  color?: string
  dataKey?: string
}

function ActivityTooltip({ active, payload, label }: { active?: boolean; payload?: TooltipPayload[]; label?: string }) {
  if (!active || !payload?.length || !label) return null
  return (
    <div className="rounded-lg border bg-popover px-3 py-2 text-xs text-popover-foreground shadow-md">
      <p className="pb-1 font-medium">{shortDay(label)}</p>
      {payload.map((entry) => (
        <p key={entry.dataKey} className="flex items-center gap-2">
          <span aria-hidden="true" className="size-2 rounded-full" style={{ background: entry.color }} />
          <span className="text-muted-foreground">{entry.name}</span>
          <span className="ml-auto pl-3 font-medium tabular-nums">{entry.value}</span>
        </p>
      ))}
    </div>
  )
}

function CategoryTooltip({ active, payload, label }: { active?: boolean; payload?: TooltipPayload[]; label?: string }) {
  if (!active || !payload?.length) return null
  return (
    <div className="rounded-lg border bg-popover px-3 py-2 text-xs text-popover-foreground shadow-md">
      <p className="pb-1 font-medium">{label}</p>
      {payload.map((entry) => (
        <p key={entry.dataKey} className="flex items-center gap-2">
          <span aria-hidden="true" className="size-2 rounded-full" style={{ background: entry.color }} />
          <span className="text-muted-foreground">{entry.name}</span>
          <span className="ml-auto pl-3 font-medium tabular-nums">{entry.value}</span>
        </p>
      ))}
    </div>
  )
}

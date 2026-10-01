import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { ArrowRightIcon, HandHeartIcon, HashIcon, MapPinIcon, PackageCheckIcon, SparklesIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { DashboardPanel, PanelDivider } from '@/components/layout/dashboard-panel'
import { Skeleton } from '@/components/ui/skeleton'
import { displayCode, timeAgo } from '@/features/feed/feed-api'
import { getHelpToFind, type HelpActivity } from './help-api'

/**
 * Everything one person has done for someone else, and what they earned for it.
 *
 * It doubles as the place a finder comes back to for the six digits they need at the desk -
 * the code is on the owner's public report, so re-reading it here gives nothing away, and
 * a finder who closed the tab has somewhere to look other than the feed.
 */
export function HelpToFindPage() {
  const { data, isPending, isError, refetch } = useQuery({
    queryKey: ['help-to-find'],
    queryFn: getHelpToFind,
  })

  return (
    <section className="flex w-full flex-col gap-6">
      <div>
        <p className="text-sm font-medium text-brand-green">Help to find</p>
        <h1 className="pt-1 text-2xl font-semibold tracking-tight">What you have done for other people</h1>
        <p className="max-w-xl pt-2 text-sm text-muted-foreground">
          Every item you offered to help with, the codes you still need at a desk, and the
          honor points you earned. Points arrive when an item actually gets home - not for
          pressing a button.
        </p>
      </div>

      {isPending ? (
        <div className="flex flex-col gap-4">
          <Skeleton className="h-28 w-full" />
          <Skeleton className="h-44 w-full" />
        </div>
      ) : isError ? (
        <DashboardPanel className="flex flex-col items-start gap-3" role="alert">
          <p className="text-sm">Could not load your finding activity.</p>
          <Button variant="outline" size="sm" onClick={() => refetch()}>Try again</Button>
        </DashboardPanel>
      ) : (
        <>
          <DashboardPanel className="grid gap-6 sm:grid-cols-4">
            <Figure
              icon={<SparklesIcon className="size-4" aria-hidden="true" />}
              value={data.honorPoints}
              label="honor points"
              emphasis
            />
            <Figure
              icon={<PackageCheckIcon className="size-4" aria-hidden="true" />}
              value={data.itemsReturned}
              label={data.itemsReturned === 1 ? 'item got home' : 'items got home'}
            />
            <Figure
              icon={<HandHeartIcon className="size-4" aria-hidden="true" />}
              value={data.handIns}
              label={data.handIns === 1 ? 'hand-in at a desk' : 'hand-ins at a desk'}
            />
            <Figure
              icon={<HashIcon className="size-4" aria-hidden="true" />}
              value={data.openHelpOffers}
              label={data.openHelpOffers === 1 ? 'code still to use' : 'codes still to use'}
            />
          </DashboardPanel>

          {data.activity.length === 0 ? (
            <DashboardPanel className="flex flex-col items-start gap-3">
              <h2 className="text-base font-medium">Nothing yet</h2>
              <p className="max-w-md text-sm text-muted-foreground">
                When you press "I found this" on someone's report, or post something you picked
                up, it shows here with the code to quote at the desk.
              </p>
              <Button variant="outline" nativeButton={false} render={<Link to="/feed" />}>
                Browse the lost board<ArrowRightIcon aria-hidden="true" />
              </Button>
            </DashboardPanel>
          ) : (
            <DashboardPanel className="flex flex-col gap-4">
              <h2 className="text-base font-medium">Your finding activity</h2>
              <PanelDivider />
              <ol className="flex flex-col">
                {data.activity.map((entry, index) => (
                  <ActivityRow key={`${entry.kind}-${entry.reportId}`} entry={entry} isFirst={index === 0} />
                ))}
              </ol>
            </DashboardPanel>
          )}
        </>
      )}
    </section>
  )
}

function Figure({
  icon,
  value,
  label,
  emphasis,
}: {
  icon: React.ReactNode
  value: number
  label: string
  emphasis?: boolean
}) {
  return (
    <div className="flex flex-col gap-1">
      <span className="flex items-center gap-2 text-muted-foreground">{icon}</span>
      <p className={emphasis ? 'text-4xl font-semibold tracking-tight tabular-nums' : 'text-3xl font-semibold tracking-tight tabular-nums'}>
        {value}
      </p>
      <p className="text-sm text-muted-foreground">{label}</p>
    </div>
  )
}

function ActivityRow({ entry, isFirst }: { entry: HelpActivity; isFirst: boolean }) {
  const isPost = entry.kind === 'found-post'

  return (
    <li className={isFirst ? 'flex flex-col gap-2 py-4 first:pt-0' : 'flex flex-col gap-2 border-t border-foreground/8 py-4'}>
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <p className="font-medium">
          {entry.itemTypeName}
          <span className="pl-2 text-sm font-normal text-muted-foreground">
            {isPost ? 'you posted this' : `${entry.ownerName ?? 'someone'}'s report`}
          </span>
        </p>
        <p className="text-sm text-muted-foreground">
          {entry.pointsEarned > 0 && <span className="font-medium text-brand-forest dark:text-brand-sage">+{entry.pointsEarned} · </span>}
          {timeAgo(entry.createdAt)}
        </p>
      </div>

      <p className="flex items-center gap-1.5 text-sm text-muted-foreground">
        <MapPinIcon className="size-3.5" aria-hidden="true" />
        {entry.locationName}
        <span aria-hidden="true">·</span>
        {entry.status}
      </p>

      {entry.handInCode && (
        <div className="flex items-center justify-between gap-4 rounded-xl bg-brand-forest px-4 py-3 text-white">
          <div>
            <p className="text-xs text-white/70">Quote this code at the desk</p>
            <p className="font-mono text-xl font-semibold tracking-[0.2em] tabular-nums">
              {displayCode(entry.handInCode)}
            </p>
          </div>
          <HashIcon className="size-5 shrink-0 text-white/60" aria-hidden="true" />
        </div>
      )}
    </li>
  )
}

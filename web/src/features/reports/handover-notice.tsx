import { useQuery } from '@tanstack/react-query'
import { HashIcon, PackageCheckIcon, TruckIcon } from 'lucide-react'
import { displayCode } from '@/features/feed/feed-api'
import { getHandover } from '@/features/feed/handover-api'

/**
 * The owner's half of a handover: somebody is bringing their item in, and this is the code
 * they will quote to collect it.
 *
 * Shown on the report's own card rather than in a notification, because a code you need at a
 * desk has to be somewhere you can find it again.
 */
export function HandoverNotice({ reportId }: { reportId: string }) {
  const { data } = useQuery({
    queryKey: ['handover', reportId],
    queryFn: () => getHandover(reportId),
    // The finder may hand it in while the owner is looking at this.
    refetchInterval: 30_000,
  })

  if (!data || (data.status !== 'AwaitingHandIn' && data.status !== 'InCustody')) return null

  const atDesk = data.status === 'InCustody'

  return (
    <div className="fu-appear flex flex-col gap-3 rounded-xl border border-brand-green/35 bg-brand-green/10 p-4">
      <p className="flex items-start gap-2.5 text-sm">
        {atDesk ? (
          <PackageCheckIcon className="mt-0.5 size-4 shrink-0 text-brand-forest dark:text-brand-sage" aria-hidden="true" />
        ) : (
          <TruckIcon className="mt-0.5 size-4 shrink-0 text-brand-forest dark:text-brand-sage" aria-hidden="true" />
        )}
        <span>
          <span className="font-medium">
            {atDesk
              ? `Ready to collect${data.storageLocationName ? ` at ${data.storageLocationName}` : ''}`
              : `${data.finderName?.split(' ')[0] ?? 'Someone'} is taking it to a desk`}
          </span>
          <span className="text-muted-foreground">
            {atDesk
              ? ' - bring your student ID and quote this code.'
              : ' - your notice is paused until they do. Quote this code when you collect it.'}
          </span>
        </span>
      </p>

      {data.code && (
        <div className="flex items-center justify-between gap-4 rounded-lg bg-brand-forest px-4 py-3 text-white">
          <div>
            <p className="text-xs text-white/70">Your collection code</p>
            <p className="font-mono text-2xl font-semibold tracking-[0.2em] tabular-nums">
              {displayCode(data.code)}
            </p>
          </div>
          <HashIcon className="size-6 shrink-0 text-white/60" aria-hidden="true" />
        </div>
      )}

      <p className="text-xs text-muted-foreground">
        Only you and the finder can see this code. The desk checks your student ID against the
        name on this report before handing anything over.
      </p>
    </div>
  )
}

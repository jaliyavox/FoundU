import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  BellIcon,
  CheckCheckIcon,
  CircleCheckIcon,
  CircleXIcon,
  HandHeartIcon,
  MapPinIcon,
  MessageSquareIcon,
  PackageSearchIcon,
  ShieldQuestionIcon,
  UndoIcon,
} from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { timeAgo } from '@/features/feed/feed-api'
import { cn } from '@/lib/utils'
import {
  getNotifications,
  getUnreadCount,
  linkFor,
  markAllRead,
  markRead,
  type AppNotification,
  type NotificationType,
} from './notifications-api'

/** An icon per event, so the list can be skimmed without reading every title. */
const ICONS: Record<NotificationType, typeof BellIcon> = {
  PossibleMatchFound: PackageSearchIcon,
  VerificationQuestionAvailable: ShieldQuestionIcon,
  RevisionRequested: UndoIcon,
  ClaimApproved: CircleCheckIcon,
  ClaimRejected: CircleXIcon,
  CollectionInstructions: MapPinIcon,
  ItemReportedFound: HandHeartIcon,
  MessageReceived: MessageSquareIcon,
}

/** Only the two that carry an outcome get colour. Everything else is just news. */
const TONES: Partial<Record<NotificationType, string>> = {
  ClaimApproved: 'text-brand-forest dark:text-brand-sage',
  ClaimRejected: 'text-destructive',
}

/**
 * The bell, and the list behind it.
 *
 * The count is polled rather than pushed - there is no socket, and a minute of staleness on
 * "someone found your bag" is honest for a campus desk. Opening a notification marks it read
 * and takes you to the thing it is about, because a list you have to clear by hand is a list
 * people stop opening.
 */
export function NotificationBell() {
  const [open, setOpen] = useState(false)
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const { data: count } = useQuery({
    queryKey: ['notification-count'],
    queryFn: getUnreadCount,
    refetchInterval: 60_000,
    refetchOnWindowFocus: true,
  })

  const { data, isPending } = useQuery({
    queryKey: ['notifications'],
    queryFn: () => getNotifications(),
    enabled: open,
  })

  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: ['notifications'] })
    queryClient.invalidateQueries({ queryKey: ['notification-count'] })
  }

  const read = useMutation({ mutationFn: markRead, onSuccess: refresh })
  const readAll = useMutation({ mutationFn: markAllRead, onSuccess: refresh })

  const unread = count?.unread ?? 0
  const items = data?.items ?? []

  function openNotification(notification: AppNotification) {
    if (!notification.isRead) read.mutate(notification.id)

    const to = linkFor(notification)
    if (to) {
      setOpen(false)
      navigate(to)
    }
  }

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger
        render={
          <Button
            variant="ghost"
            size="icon-sm"
            className="relative"
            aria-label={unread > 0 ? `Notifications, ${unread} unread` : 'Notifications'}
          >
            <BellIcon className="size-4" aria-hidden="true" />
            {unread > 0 && (
              <span
                aria-hidden="true"
                className="absolute -top-0.5 -right-0.5 flex min-w-4 items-center justify-center rounded-full bg-brand-green px-1 text-[10px] leading-4 font-semibold text-brand-forest tabular-nums"
              >
                {unread > 9 ? '9+' : unread}
              </span>
            )}
          </Button>
        }
      />

      <PopoverContent align="end" className="w-88 p-0">
        <div className="flex items-center justify-between gap-2 border-b p-3">
          <p className="text-sm font-medium">Notifications</p>
          {unread > 0 && (
            <Button
              variant="ghost"
              size="sm"
              className="text-muted-foreground"
              onClick={() => readAll.mutate()}
              disabled={readAll.isPending}
            >
              <CheckCheckIcon aria-hidden="true" />
              Mark all read
            </Button>
          )}
        </div>

        <div className="max-h-96 overflow-y-auto">
          {isPending ? (
            <p className="p-6 text-center text-sm text-muted-foreground">Loading...</p>
          ) : items.length === 0 ? (
            <div className="flex flex-col items-center gap-2 p-8 text-center">
              <BellIcon className="size-5 text-muted-foreground" aria-hidden="true" />
              <p className="text-sm font-medium">Nothing yet</p>
              <p className="text-xs text-muted-foreground">
                You will hear here when someone finds something you reported.
              </p>
            </div>
          ) : (
            <ul className="divide-y">
              {items.map((notification) => {
                const Icon = ICONS[notification.type] ?? BellIcon

                return (
                  <li key={notification.id}>
                    <button
                      type="button"
                      onClick={() => openNotification(notification)}
                      className={cn(
                        'flex w-full items-start gap-3 p-3 text-left transition-colors hover:bg-muted/60 focus-visible:bg-muted/60 focus-visible:outline-none',
                        !notification.isRead && 'bg-brand-green/6',
                      )}
                    >
                      <Icon
                        className={cn(
                          'mt-0.5 size-4 shrink-0',
                          TONES[notification.type] ?? 'text-muted-foreground',
                        )}
                        aria-hidden="true"
                      />

                      <div className="min-w-0 flex-1">
                        <p
                          className={cn(
                            'text-sm',
                            notification.isRead ? 'text-muted-foreground' : 'font-medium',
                          )}
                        >
                          {notification.title}
                        </p>
                        <p className="text-xs text-pretty text-muted-foreground">
                          {notification.message}
                        </p>
                        <p className="pt-1 text-xs text-muted-foreground/70">
                          {timeAgo(notification.createdAt)}
                        </p>
                      </div>

                      {!notification.isRead && (
                        <span
                          aria-hidden="true"
                          className="mt-1.5 size-2 shrink-0 rounded-full bg-brand-green"
                        />
                      )}
                    </button>
                  </li>
                )
              })}
            </ul>
          )}
        </div>
      </PopoverContent>
    </Popover>
  )
}

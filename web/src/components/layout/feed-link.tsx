import { Link } from 'react-router-dom'
import { BackpackIcon, KeyRoundIcon, NewspaperIcon, SmartphoneIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'

/**
 * Header link to the public feed, drawn as a little network: a cluster of item nodes with a
 * dashed line flowing into the button.
 *
 * The motion lives on the connector rather than the button - animating both competed with
 * itself. The cluster collapses on narrow screens, leaving just the button.
 */

const ITEM_NODES = [BackpackIcon, SmartphoneIcon, KeyRoundIcon]

export function FeedLink() {
  return (
    <div className="ml-auto flex items-center">
      {/* Item nodes, overlapped like a stack. */}
      <div className="hidden items-center md:flex" aria-hidden="true">
        {ITEM_NODES.map((Icon, index) => (
          <span
            key={index}
            className="-ml-2 flex size-8 items-center justify-center rounded-full bg-background ring-1 ring-border first:ml-0"
            style={{ zIndex: ITEM_NODES.length - index }}
          >
            <Icon className="size-3.5 text-muted-foreground" />
          </span>
        ))}
      </div>

      {/* Dashes travel toward the button, so the eye follows into it. */}
      <svg
        aria-hidden="true"
        viewBox="0 0 48 24"
        className="hidden h-6 w-12 shrink-0 text-brand-green/70 md:block"
        preserveAspectRatio="none"
      >
        <path
          className="fu-flow"
          d="M0 12 H48"
          fill="none"
          stroke="currentColor"
          strokeWidth="1.5"
          strokeLinecap="round"
        />
      </svg>

      <Button
        size="sm"
        className="group gap-2 rounded-full bg-primary px-4 font-medium text-primary-foreground shadow-sm transition-shadow duration-300 hover:shadow-md"
        nativeButton={false}
        render={<Link to="/feed" />}
      >
        <NewspaperIcon aria-hidden="true" />
        <span className="hidden sm:inline">Lost feed</span>
      </Button>
    </div>
  )
}

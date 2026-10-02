import { SparklesIcon } from 'lucide-react'

/** On a ticket sent from the support assistant's draft: the desk knows what was already tried. */
export function AssistantBadge() {
  return (
    <span className="inline-flex items-center gap-1 rounded-full bg-brand-forest/10 px-2 py-0.5 text-[11px] font-medium text-brand-forest dark:bg-brand-green/15 dark:text-brand-sage">
      <SparklesIcon className="size-3" aria-hidden="true" />
      Assistant tried first
    </span>
  )
}

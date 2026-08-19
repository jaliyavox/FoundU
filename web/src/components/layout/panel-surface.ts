/**
 * The dashboard's one surface treatment: a rounded card lit by a white gradient that is
 * strongest at the top edge and fades down, over a hairline white stroke.
 *
 * Kept in its own module rather than beside the component so cards, the sidebar and the
 * table can wear the same surface without importing a component they do not render - and
 * so a change to the look lands in one place.
 */
export const panelSurface =
  'relative overflow-hidden rounded-2xl border border-foreground/8 bg-linear-to-b from-white via-white/85 to-white/55 backdrop-blur-sm dark:border-white/10 dark:from-white/[0.08] dark:via-white/[0.04] dark:to-white/[0.015]'

/** The stroke on its own, for surfaces that bring their own background. */
export const panelStroke =
  'pointer-events-none absolute inset-x-0 top-0 h-px bg-linear-to-r from-transparent via-white/80 to-transparent dark:via-white/30'

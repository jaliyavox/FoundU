import { BellRingIcon, CheckIcon, EyeIcon, EyeOffIcon, MapPinIcon, SearchIcon } from 'lucide-react'
import { BentoCard, DarkSection, GradientDivider, SectionHeading } from './bento'
import { cn } from '@/lib/utils'

/* -------------------------------------------------------------------------- */
/*  Visuals. Illustrative UI rather than measured figures.                      */
/* -------------------------------------------------------------------------- */

/** Who can see the identifying details - the heart of the verification design. */
const VISIBILITY_ROWS = [
  { role: 'Students', value: 'Hidden', icon: EyeOffIcon, allowed: false },
  { role: 'Desk staff', value: 'Visible', icon: EyeIcon, allowed: true },
]

function VisibilityVisual() {
  return (
    <div className="flex h-full flex-col justify-center gap-2.5">
      <p className="text-[11px] tracking-wide text-white/35 uppercase">
        “Red USB stick in the front pocket”
      </p>

      {VISIBILITY_ROWS.map(({ role, value, icon: Icon, allowed }) => (
        <div
          key={role}
          className={cn(
            'flex items-center justify-between rounded-xl border px-3 py-2.5',
            allowed
              ? 'border-brand-green/35 bg-brand-green/10'
              : 'border-white/10 bg-white/[0.03]',
          )}
        >
          <span className="text-xs text-white/75">{role}</span>
          <span
            className={cn(
              'flex items-center gap-1.5 text-xs',
              allowed ? 'text-brand-green' : 'text-white/35',
            )}
          >
            <Icon className="size-3.5" />
            {value}
          </span>
        </div>
      ))}
    </div>
  )
}

/** Reports arriving and being compared, rather than a person checking back daily. */
const INCOMING = ['Navy backpack · Library', 'Keys, red fob · Cafeteria', 'Headphones · B12']

function MatchingVisual() {
  return (
    <div className="relative flex h-full items-center gap-4">
      <div className="flex flex-1 flex-col gap-2">
        {INCOMING.map((item, index) => (
          <div
            key={item}
            className="fu-node-in truncate rounded-lg border border-white/10 bg-white/[0.04] px-3 py-2 text-[11px] text-white/60"
            style={{ animationDelay: `${index * 160}ms` }}
          >
            {item}
          </div>
        ))}
      </div>

      <svg viewBox="0 0 60 90" className="h-24 w-14 shrink-0" aria-hidden="true">
        <g fill="none" stroke="currentColor" className="text-brand-green/45" strokeWidth="1.3">
          <path className="fu-flow" d="M2 14 C 30 14, 30 45, 56 45" />
          <path className="fu-flow" d="M2 45 H 56" style={{ animationDelay: '0.9s' }} />
          <path className="fu-flow" d="M2 76 C 30 76, 30 45, 56 45" style={{ animationDelay: '1.7s' }} />
        </g>
      </svg>

      <span className="relative flex size-12 shrink-0 items-center justify-center rounded-xl border border-brand-green/40 bg-brand-green/15">
        <SearchIcon className="size-5 text-brand-green" />
        <span className="fu-ping absolute inset-0 rounded-xl border border-brand-green/50" />
      </span>
    </div>
  )
}

/** One place instead of six desks. */
const PLACES = ['Library', 'Cafeteria', 'Hall B12', 'Sports Complex', 'Main Auditorium', 'Parking Lot']

function CoverageVisual() {
  return (
    <div className="flex h-full flex-wrap content-center gap-2">
      {PLACES.map((place, index) => (
        <span
          key={place}
          className="fu-node-in flex items-center gap-1.5 rounded-full border border-white/12 bg-white/[0.05] px-3 py-1.5 text-[11px] text-white/70"
          style={{ animationDelay: `${index * 90}ms` }}
        >
          <MapPinIcon className="size-3 text-brand-green/80" />
          {place}
        </span>
      ))}
    </div>
  )
}

/** The notification that saves the repeat trip. */
function NotifyVisual() {
  return (
    <div className="flex h-full flex-col justify-center gap-2">
      <div className="rounded-xl border border-brand-green/35 bg-brand-green/10 p-3">
        <div className="flex items-center gap-2">
          <span className="relative flex size-6 items-center justify-center rounded-lg bg-brand-green/25">
            <BellRingIcon className="size-3.5 text-brand-green" />
            <span className="fu-ping absolute inset-0 rounded-lg border border-brand-green/60" />
          </span>
          <span className="text-xs text-white/85">Possible match found</span>
        </div>
        <p className="pt-1.5 pl-8 text-[11px] text-white/50">Navy backpack · Security Desk A</p>
      </div>

      <div className="flex items-center gap-2 rounded-xl border border-white/10 bg-white/[0.03] p-3 opacity-70">
        <span className="flex size-6 items-center justify-center rounded-lg bg-white/10">
          <CheckIcon className="size-3.5 text-white/50" />
        </span>
        <span className="text-xs text-white/50">Claim approved</span>
      </div>
    </div>
  )
}

/* -------------------------------------------------------------------------- */

export function Features() {
  return (
    <DarkSection id="features" labelledBy="features-heading">
      <GradientDivider />

      <div className="mx-auto w-full max-w-6xl px-6 py-20 sm:py-28">
        <SectionHeading
          id="features-heading"
          eyebrow="Why FoundU"
          title="Built for how campus lost property actually works"
          body="Six desks, a paper logbook and a lot of hoping is not a system. This is."
        />

        {/* Mirrors the How-it-works rhythm rather than repeating it: 1+2, then 2+1. */}
        <ul className="mt-14 grid gap-4 lg:grid-cols-3">
          <li>
            <BentoCard
              align="left"
              eyebrow="Verification"
              title="Ownership you have to prove"
              body="Identifying details are recorded but never shown to a claimant. You describe them from memory instead."
              visual={<VisibilityVisual />}
              className="h-full"
            />
          </li>

          <li className="lg:col-span-2">
            <BentoCard
              align="left"
              eyebrow="Matching"
              title="Matching that does the looking"
              body="New reports are read as they arrive and compared against everything handed in, so you are not checking back every day."
              visual={<MatchingVisual />}
              className="h-full"
            />
          </li>

          <li className="lg:col-span-2">
            <BentoCard
              align="left"
              eyebrow="Coverage"
              title="Every corner of campus"
              body="Lecture halls, the library, the cafeteria, the sports complex. One place to look instead of six separate desks."
              visual={<CoverageVisual />}
              className="h-full"
            />
          </li>

          <li>
            <BentoCard
              align="left"
              eyebrow="Notifications"
              title="Told the moment it turns up"
              body="A notification when something matching your report is handed in. No repeat trips, no standing in queues."
              visual={<NotifyVisual />}
              className="h-full"
            />
          </li>
        </ul>
      </div>
    </DarkSection>
  )
}

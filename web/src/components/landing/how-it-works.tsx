import { CheckIcon, LockIcon, MapPinIcon } from 'lucide-react'
import { BentoCard, DarkSection, GradientDivider, SectionHeading } from './bento'
import { cn } from '@/lib/utils'

/* -------------------------------------------------------------------------- */
/*  Visuals. Illustrative UI, not real figures - there is no anonymous feed of  */
/*  found items, so nothing here is claimed as a statistic.                     */
/* -------------------------------------------------------------------------- */

const ATTRIBUTE_PILLS = ['Backpack', 'Navy', 'Library', '2-3pm', 'Laptop sleeve']

const CAMPUS_PINS = [
  { label: 'Library', x: 26, y: 33 },
  { label: 'Cafeteria', x: 62, y: 62 },
  { label: 'Hall B12', x: 78, y: 27 },
]

/** Dot-grid campus plane with the places a report touches. */
function CampusPlaneVisual() {
  const dots = []
  // 4:1 viewBox - a taller grid stretched across the wide card left a lot of dead space.
  for (let row = 0; row < 10; row += 1) {
    for (let col = 0; col < 40; col += 1) {
      dots.push(<circle key={`${row}-${col}`} cx={col * 16 + 8} cy={row * 16 + 8} r="1.1" />)
    }
  }

  return (
    // No h-full here: it used to consume the whole visual slot and push the pills out
    // of the card, over the heading below.
    <div className="relative">
      <svg
        viewBox="0 0 640 160"
        className="w-full text-white/25"
        style={{
          maskImage: 'radial-gradient(ellipse 62% 72% at 50% 48%, black, transparent)',
          WebkitMaskImage: 'radial-gradient(ellipse 62% 72% at 50% 48%, black, transparent)',
        }}
      >
        <g fill="currentColor">{dots}</g>

        <g fill="none" stroke="currentColor" className="text-brand-green/55" strokeWidth="1.6">
          <path className="fu-flow" d="M214 62 C 292 62, 312 96, 372 100" />
          <path className="fu-flow" d="M432 96 C 472 92, 472 58, 498 50" style={{ animationDelay: '1.4s' }} />
        </g>
      </svg>

      {CAMPUS_PINS.map(({ label, x, y }, index) => (
        <span
          key={label}
          className="fu-node-in absolute flex -translate-x-1/2 -translate-y-1/2 items-center gap-1.5 rounded-full border border-white/15 bg-white/10 py-1 pr-2.5 pl-1.5 text-[11px] whitespace-nowrap text-white/80 backdrop-blur-md"
          style={{ left: `${x}%`, top: `${y}%`, animationDelay: `${index * 140}ms` }}
        >
          <span className="relative flex size-4 items-center justify-center rounded-full bg-brand-green/20">
            <MapPinIcon className="size-2.5 text-brand-green" />
            <span className="fu-ping absolute inset-0 rounded-full border border-brand-green/60" />
          </span>
          {label}
        </span>
      ))}
    </div>
  )
}

/** Candidate found items, ranked. Percentages are sample UI, not measured accuracy. */
const MATCH_BARS = [
  { height: 46, score: 41 },
  { height: 62, score: 55 },
  { height: 88, score: 79 },
  { height: 100, score: 92 },
  { height: 54, score: 48 },
  { height: 36, score: 32 },
]

function MatchBarsVisual() {
  return (
    <div className="flex h-full items-end justify-center gap-2.5 pt-4">
      {MATCH_BARS.map(({ height, score }, index) => (
        <div key={score} className="flex flex-col items-center gap-2">
          <span
            className={cn(
              'size-2 rounded-[3px] transition-colors duration-500',
              score >= 79 ? 'bg-brand-green' : 'bg-white/25',
            )}
          />
          <div
            className={cn(
              'w-6 rounded-t-md transition-all duration-700 ease-out',
              score >= 79
                ? 'bg-linear-to-t from-brand-green/15 to-brand-green/80'
                : 'bg-linear-to-t from-white/5 to-white/20',
            )}
            style={{ height: `${height}px`, transitionDelay: `${index * 60}ms` }}
          />
        </div>
      ))}
    </div>
  )
}

/** The hidden-evidence check: staff hold the answer, the claimant has to supply it. */
function VerifyVisual() {
  return (
    <div className="flex h-full flex-col justify-center gap-2.5">
      <div className="rounded-xl border border-white/10 bg-white/[0.04] p-3">
        <p className="text-[11px] text-white/45">What is inside the front pocket?</p>
        <div className="flex items-center gap-1.5 pt-2">
          <LockIcon className="size-3 text-white/35" />
          <span className="text-xs tracking-[0.2em] text-white/35">••••••••••</span>
        </div>
      </div>

      <div className="rounded-xl border border-brand-green/35 bg-brand-green/10 p-3">
        <p className="text-[11px] text-white/55">Claimant answered</p>
        <div className="flex items-center gap-1.5 pt-2">
          <span className="flex size-4 items-center justify-center rounded-full bg-brand-green">
            <CheckIcon className="size-2.5 text-[oklch(0.17_0.028_148)]" strokeWidth={3.5} />
          </span>
          <span className="text-xs text-white/85">A red USB stick</span>
        </div>
      </div>
    </div>
  )
}

const HANDOVER_STAGES = ['Reported', 'Matched', 'Verified', 'Collected']

function HandoverVisual() {
  return (
    <div className="flex h-full flex-col justify-center gap-5 px-1">
      <div className="relative">
        <div className="h-0.5 w-full rounded-full bg-white/10" />
        <div className="absolute inset-y-0 left-0 h-0.5 w-full rounded-full bg-linear-to-r from-brand-green/30 to-brand-green" />

        <div className="absolute inset-x-0 -top-[7px] flex justify-between">
          {HANDOVER_STAGES.map((stage, index) => (
            <span key={stage} className="relative flex size-4 items-center justify-center">
              <span
                className={cn(
                  'size-4 rounded-full border-2 border-[oklch(0.17_0.028_148)]',
                  index === HANDOVER_STAGES.length - 1 ? 'bg-brand-green' : 'bg-brand-green/70',
                )}
              />
              {index === HANDOVER_STAGES.length - 1 && (
                <span className="fu-ping absolute inset-0 rounded-full border border-brand-green" />
              )}
            </span>
          ))}
        </div>
      </div>

      <div className="flex justify-between">
        {HANDOVER_STAGES.map((stage, index) => (
          <span
            key={stage}
            className={cn(
              'text-[11px]',
              index === HANDOVER_STAGES.length - 1 ? 'text-white/85' : 'text-white/40',
            )}
          >
            {stage}
          </span>
        ))}
      </div>
    </div>
  )
}

/* -------------------------------------------------------------------------- */

export function HowItWorks() {
  return (
    <DarkSection id="how-it-works" labelledBy="how-it-works-heading">
      <GradientDivider />
      <div className="mx-auto w-full max-w-6xl px-6 py-20 sm:py-28">
        <SectionHeading
          id="how-it-works-heading"
          eyebrow="How it works"
          title="From lost to back in your hands"
          body="Describe it once. FoundU does the searching, checks the claim is genuine, and tells you where to collect."
        />

        <ol className="mt-14 grid gap-4 lg:grid-cols-3">
          <li className="lg:col-span-2">
            <BentoCard
              step={1}
              title="Describe it once"
              body="Category, colour, roughly where and roughly when. Every detail narrows the search across campus."
              visual={
                <div className="flex h-full flex-col justify-center gap-5">
                  <CampusPlaneVisual />
                  <div className="flex flex-wrap justify-center gap-1.5">
                    {ATTRIBUTE_PILLS.map((pill) => (
                      <span
                        key={pill}
                        className="rounded-full border border-white/12 bg-white/[0.06] px-2.5 py-1 text-[11px] text-white/70"
                      >
                        {pill}
                      </span>
                    ))}
                  </div>
                </div>
              }
              className="h-full"
            />
          </li>

          <li>
            <BentoCard
              step={2}
              title="Ranked, not searched"
              body="Your report is compared against everything handed in, and the closest candidates rise to the top."
              visual={<MatchBarsVisual />}
              className="h-full"
            />
          </li>

          <li>
            <BentoCard
              step={3}
              title="Only the owner knows"
              body="Identifying details stay hidden. You describe them from memory, and staff confirm the match."
              visual={<VerifyVisual />}
              className="h-full"
            />
          </li>

          <li className="lg:col-span-2">
            <BentoCard
              step={4}
              title="Collected, and closed"
              body="You are told which desk is holding it and when it is ready. Every step stays on the record."
              visual={<HandoverVisual />}
              className="h-full"
            />
          </li>
        </ol>
      </div>
    </DarkSection>
  )
}

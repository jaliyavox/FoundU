import type { CSSProperties } from 'react'
import { Link } from 'react-router-dom'
import {
  ArrowDownIcon,
  ArrowRightIcon,
  BackpackIcon,
  HeadphonesIcon,
  KeyRoundIcon,
  SmartphoneIcon,
  WalletIcon,
} from 'lucide-react'
import { Button } from '@/components/ui/button'
import { useRotatingIndex } from '@/hooks/use-rotating-index'
import { cn } from '@/lib/utils'

/**
 * Illustrative sample data. There is no anonymous endpoint for found items - /api/found-reports
 * is Staff-only by design - so these are examples of the kinds of thing that get handed in,
 * not live figures. Swap for a real feed if a public "recently handed in" endpoint is added.
 */
const ITEM_NODES = [
  { icon: BackpackIcon, label: 'Backpack', place: 'Library', className: 'top-[18%] left-[4%]', delay: 200 },
  { icon: SmartphoneIcon, label: 'Phone', place: 'Lecture Hall B12', className: 'top-[14%] right-[5%]', delay: 340 },
  { icon: KeyRoundIcon, label: 'Keys', place: 'Cafeteria', className: 'bottom-[22%] left-[7%]', delay: 480 },
  { icon: WalletIcon, label: 'Wallet', place: 'Sports Complex', className: 'bottom-[18%] right-[6%]', delay: 620 },
]

const TICKER = [
  { icon: BackpackIcon, text: 'Navy backpack', place: 'Library' },
  { icon: HeadphonesIcon, text: 'Wireless headphones', place: 'Main Auditorium' },
  { icon: KeyRoundIcon, text: 'Keys with a red fob', place: 'Cafeteria' },
  { icon: WalletIcon, text: 'Brown leather wallet', place: 'Parking Lot' },
]

export function Hero({
  ctaHref,
  primaryLabel,
}: {
  ctaHref: string
  primaryLabel: string
}) {
  const tickerIndex = useRotatingIndex(TICKER.length)
  const current = TICKER[tickerIndex]
  const CurrentIcon = current.icon

  return (
    <section className="relative isolate flex min-h-svh flex-col overflow-hidden bg-[oklch(0.17_0.028_148)] text-white">
      {/* --- animated aurora ------------------------------------------------ */}
      <div aria-hidden="true" className="pointer-events-none absolute inset-0 -z-10">
        <div className="fu-aurora-a absolute -top-1/4 left-[8%] size-[46rem] rounded-full bg-brand-forest/70 blur-[120px]" />
        <div className="fu-aurora-b absolute top-[10%] right-[2%] size-[38rem] rounded-full bg-brand-green/35 blur-[130px]" />
        <div className="fu-aurora-c absolute -bottom-1/4 left-[28%] size-[40rem] rounded-full bg-brand-sage/20 blur-[140px]" />
        {/* faint grid, fading out towards the edges */}
        <div
          className="absolute inset-0 opacity-[0.07]"
          style={{
            backgroundImage:
              'linear-gradient(to right, white 1px, transparent 1px), linear-gradient(to bottom, white 1px, transparent 1px)',
            backgroundSize: '64px 64px',
            maskImage: 'radial-gradient(ellipse 70% 60% at 50% 45%, black, transparent)',
          }}
        />
      </div>

      {/* --- connector lines ------------------------------------------------ */}
      <svg
        aria-hidden="true"
        viewBox="0 0 1200 800"
        preserveAspectRatio="none"
        className="pointer-events-none absolute inset-0 -z-10 hidden h-full w-full text-white/25 lg:block"
      >
        <g fill="none" stroke="currentColor" strokeWidth="1.5">
          <path className="fu-flow" d="M120 190 C 300 190, 380 330, 560 360" />
          <path className="fu-flow" d="M1080 150 C 900 150, 820 320, 640 360" style={{ animationDelay: '1.2s' }} />
          <path className="fu-flow" d="M140 620 C 320 620, 400 470, 560 430" style={{ animationDelay: '2.1s' }} />
          <path className="fu-flow" d="M1060 650 C 880 650, 800 480, 640 430" style={{ animationDelay: '0.6s' }} />
        </g>
      </svg>

      {/* --- floating item nodes -------------------------------------------- */}
      <div aria-hidden="true" className="pointer-events-none absolute inset-0 -z-10 hidden lg:block">
        {ITEM_NODES.map(({ icon: Icon, label, place, className, delay }) => (
          <div
            key={label}
            className={cn('fu-node-in absolute flex items-center gap-3', className)}
            style={{ animationDelay: `${delay}ms` } as CSSProperties}
          >
            <span className="relative flex size-11 items-center justify-center rounded-full border border-white/20 bg-white/8 backdrop-blur-md">
              <Icon className="size-5 text-brand-sage" />
              <span className="fu-ping absolute inset-0 rounded-full border border-brand-green/60" />
            </span>
            <span className="grid leading-tight">
              <span className="text-sm font-medium text-white/90">{label}</span>
              <span className="text-xs text-white/45">{place}</span>
            </span>
          </div>
        ))}
      </div>

      {/* --- centre column --------------------------------------------------- */}
      <div className="relative mx-auto flex w-full max-w-4xl flex-1 flex-col items-center justify-center gap-7 px-6 pt-32 pb-28 text-center sm:pt-36">
        <span className="inline-flex items-center gap-2 rounded-full border border-white/15 bg-white/8 px-3.5 py-1.5 text-xs font-medium text-white/85 backdrop-blur-md">
          <span className="relative flex size-1.5">
            <span className="fu-ping absolute inline-flex size-full rounded-full bg-brand-green" />
            <span className="relative inline-flex size-1.5 rounded-full bg-brand-green" />
          </span>
          Matching across campus, all day
          <ArrowRightIcon className="size-3" aria-hidden="true" />
        </span>

        <h1 className="text-4xl font-semibold tracking-tight text-balance sm:text-6xl lg:text-7xl">
          Lost it here?{' '}
          <span className="bg-linear-to-r from-white via-brand-sage to-brand-green/70 bg-clip-text text-transparent">
            We&rsquo;ll find it.
          </span>
        </h1>

        <p className="max-w-xl text-base text-pretty text-white/65 sm:text-lg">
          FoundU connects the things students lose with the things staff find — describe it once,
          and let the matching do the walking.
        </p>

        <div className="flex flex-col gap-3 pt-1 sm:flex-row sm:items-center">
          <Button
            size="lg"
            className="group rounded-xl bg-white text-brand-forest hover:bg-white/90"
            nativeButton={false}
            render={<Link to={ctaHref} />}
          >
            {primaryLabel}
            <ArrowRightIcon
              aria-hidden="true"
              className="transition-transform duration-200 group-hover:translate-x-0.5"
            />
          </Button>

          <Button
            size="lg"
            variant="outline"
            className="rounded-xl border-white/20 bg-white/8 text-white backdrop-blur-md hover:bg-white/15 hover:text-white"
            nativeButton={false}
            render={<a href="#how-it-works" />}
          >
            See how it works
          </Button>
        </div>
      </div>

      {/* --- footer rail ----------------------------------------------------- */}
      <div className="relative mx-auto flex w-full max-w-6xl flex-col items-center gap-4 px-6 pb-8 sm:flex-row sm:justify-between">
        <a
          href="#how-it-works"
          className="group inline-flex items-center gap-2.5 rounded-full border border-white/15 bg-white/8 py-1.5 pr-4 pl-1.5 text-sm text-white/75 backdrop-blur-md transition-colors hover:bg-white/15 hover:text-white focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none"
        >
          <span className="flex size-8 items-center justify-center rounded-full border border-white/20">
            <ArrowDownIcon className="size-4 transition-transform duration-200 group-hover:translate-y-0.5" />
          </span>
          Scroll to explore
        </a>

        <div className="flex w-full max-w-xs flex-col gap-2 sm:w-auto">
          <div className="flex items-center gap-2.5">
            <span className="text-xs tracking-wide text-white/45 uppercase">Recently handed in</span>
          </div>

          <div key={tickerIndex} className="fu-swap-in flex items-center gap-2.5">
            <span className="flex size-8 shrink-0 items-center justify-center rounded-lg border border-white/15 bg-white/8">
              <CurrentIcon className="size-4 text-brand-sage" aria-hidden="true" />
            </span>
            <span className="grid leading-tight">
              <span className="truncate text-sm text-white/90">{current.text}</span>
              <span className="truncate text-xs text-white/45">{current.place}</span>
            </span>
          </div>

          <div className="flex gap-1.5" aria-hidden="true">
            {TICKER.map((item, index) => (
              <span
                key={item.text}
                className={cn(
                  'h-0.5 flex-1 rounded-full transition-colors duration-500',
                  index === tickerIndex ? 'bg-brand-green' : 'bg-white/15',
                )}
              />
            ))}
          </div>
        </div>
      </div>
    </section>
  )
}

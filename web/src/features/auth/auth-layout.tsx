import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import {
  ClipboardListIcon,
  HandshakeIcon,
  ScanSearchIcon,
  ShieldCheckIcon,
} from 'lucide-react'
import { FoundULogo } from '@/components/brand/foundu-logo'
import { useRotatingIndex } from '@/hooks/use-rotating-index'
import { cn } from '@/lib/utils'

/**
 * Split auth shell: form on the left, brand panel on the right. Shared by sign-in and
 * sign-up so the two pages cannot drift apart.
 *
 * The panel is deliberately sparse: a headline and the four-step process reduced to labels
 * with a travelling highlight. An auth page competes with the form for attention, so the
 * panel shows what happens rather than explaining it.
 */

const STEPS = [
  { icon: ClipboardListIcon, label: 'Report it' },
  { icon: ScanSearchIcon, label: 'We match it' },
  { icon: ShieldCheckIcon, label: 'Prove it is yours' },
  { icon: HandshakeIcon, label: 'Collect it' },
]

export function AuthLayout({
  title,
  subtitle,
  children,
  footer,
}: {
  title: string
  subtitle: string
  children: ReactNode
  footer: ReactNode
}) {
  // Walks the four steps so the panel shows the process rather than describing it.
  const activeStep = useRotatingIndex(STEPS.length, 2300)

  return (
    <div className="flex min-h-svh flex-col bg-background lg:flex-row">
      {/* ------------------------------------------------------------ form side */}
      <div className="flex flex-1 px-6 py-8 sm:px-10">
        {/* One centred column so the logo, form and footer share an edge instead of the
            content hugging the left padding of a much wider parent. */}
        <div className="mx-auto flex w-full max-w-md flex-col">
          <Link
            to="/"
            className="inline-flex w-fit rounded-lg focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none"
            aria-label="FoundU home"
          >
            <FoundULogo markClassName="size-9" />
          </Link>

          <div className="flex flex-1 items-center py-12">
            <div className="w-full">
              <h1 className="text-3xl font-semibold tracking-tight text-balance">{title}</h1>
              <p className="pt-2 text-sm text-pretty text-muted-foreground">{subtitle}</p>

              <div className="pt-8">{children}</div>
            </div>
          </div>

          <div className="text-sm text-muted-foreground">{footer}</div>
        </div>
      </div>

      {/* ----------------------------------------------------------- brand side */}
      <div className="relative hidden overflow-hidden bg-[oklch(0.17_0.028_148)] text-white lg:flex lg:w-[46%] lg:items-center lg:justify-center lg:p-12 xl:w-1/2">
        <div aria-hidden="true" className="pointer-events-none absolute inset-0">
          <div className="fu-aurora-a absolute -top-32 -left-20 size-[32rem] rounded-full bg-brand-forest/70 blur-[120px]" />
          <div className="fu-aurora-b absolute right-[-10%] bottom-[-10%] size-[28rem] rounded-full bg-brand-green/30 blur-[120px]" />
          <div
            className="absolute inset-0 opacity-[0.06]"
            style={{
              backgroundImage:
                'linear-gradient(to right, white 1px, transparent 1px), linear-gradient(to bottom, white 1px, transparent 1px)',
              backgroundSize: '56px 56px',
              maskImage: 'radial-gradient(ellipse 80% 60% at 50% 45%, black, transparent)',
              WebkitMaskImage: 'radial-gradient(ellipse 80% 60% at 50% 45%, black, transparent)',
            }}
          />
        </div>

        <div className="relative w-full max-w-sm">
          <h2 className="text-3xl font-semibold tracking-tight text-balance xl:text-4xl">
            Lost it here?{' '}
            <span className="bg-linear-to-r from-white to-brand-green/70 bg-clip-text text-transparent">
              We&rsquo;ll find it.
            </span>
          </h2>

          {/* How it works, reduced to four labels and a travelling highlight. */}
          <ol className="relative mt-12 flex flex-col gap-7">
            {/* rail */}
            <span
              aria-hidden="true"
              className="absolute top-5 bottom-5 left-5 w-px -translate-x-1/2 bg-white/12"
            />
            <span
              aria-hidden="true"
              className="absolute top-5 left-5 w-px -translate-x-1/2 bg-brand-green transition-[height] duration-700 ease-out"
              style={{ height: `calc((100% - 2.5rem) * ${activeStep / (STEPS.length - 1)})` }}
            />

            {STEPS.map(({ icon: Icon, label }, index) => {
              const isActive = index === activeStep
              const isDone = index < activeStep

              return (
                <li key={label} className="relative flex items-center gap-4">
                  <span
                    className={cn(
                      'relative flex size-10 shrink-0 items-center justify-center rounded-xl border transition-colors duration-500',
                      isActive
                        ? 'border-brand-green/50 bg-brand-green/20 text-brand-green'
                        : isDone
                          ? 'border-brand-green/25 bg-brand-green/10 text-brand-green/70'
                          : 'border-white/12 bg-white/[0.05] text-white/40',
                    )}
                  >
                    <Icon className="size-4.5" aria-hidden="true" />
                    {isActive && (
                      <span className="fu-ping absolute inset-0 rounded-xl border border-brand-green/60" />
                    )}
                  </span>

                  <span
                    className={cn(
                      'text-sm transition-colors duration-500',
                      isActive ? 'font-medium text-white/90' : 'text-white/45',
                    )}
                  >
                    {label}
                  </span>
                </li>
              )
            })}
          </ol>
        </div>
      </div>
    </div>
  )
}

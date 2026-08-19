import { Link } from 'react-router-dom'
import { ArrowRightIcon, ClockIcon, MapPinIcon, ShieldCheckIcon } from 'lucide-react'
import { FoundUMark } from '@/components/brand/foundu-logo'
import { DarkSection, GradientDivider, VerticalDivider } from '@/components/landing/bento'
import { Hero } from '@/components/landing/hero'
import { Faq } from '@/components/landing/faq'
import { Features } from '@/components/landing/features'
import { HowItWorks } from '@/components/landing/how-it-works'
import { ParallaxDivider } from '@/components/landing/parallax-divider'
import { SiteNav } from '@/components/landing/site-nav'
import { Button } from '@/components/ui/button'
import { useAuth } from '@/features/auth/use-auth'
import { homeRouteForRole } from '@/routes/role-home'

const TRUST_POINTS = [
  { icon: ClockIcon, label: 'Report in under a minute' },
  { icon: ShieldCheckIcon, label: 'Verified before handover' },
  { icon: MapPinIcon, label: 'Campus-wide' },
]

export function LandingPage() {
  const { user } = useAuth()

  // The page stays reachable when signed in - only the calls to action change.
  const ctaHref = user ? homeRouteForRole(user.role) : '/login'
  const headerLabel = user ? 'Dashboard' : 'Sign in'
  const heroLabel = user ? 'Go to your dashboard' : 'Report a lost item'
  const closingLabel = user ? 'Go to your dashboard' : 'Get started'

  return (
    <div className="flex min-h-svh flex-col bg-[oklch(0.17_0.028_148)] text-white">
      <SiteNav ctaHref={ctaHref} ctaLabel={headerLabel} />

      <main className="flex-1">
        <Hero ctaHref={ctaHref} primaryLabel={heroLabel} />

        {/* Capability strip - deliberately claims, not invented statistics. The white wash
            and fading rules separate it from the hero without a hard border. */}
        <section aria-label="At a glance" className="relative isolate overflow-hidden">
          <div aria-hidden="true" className="pointer-events-none absolute inset-0 -z-10">
            {/* Light spilling up from the seam, brightest at the centre. */}
            <div
              className="absolute inset-0"
              style={{
                background:
                  'radial-gradient(ellipse 60% 130% at 50% 0%, rgba(255,255,255,0.13), transparent 70%)',
              }}
            />
            <div
              className="absolute inset-0"
              style={{
                background:
                  'linear-gradient(to bottom, rgba(255,255,255,0.06), rgba(255,255,255,0.01))',
              }}
            />
          </div>

          <GradientDivider className="via-white/45" />

          <div className="mx-auto w-full max-w-6xl px-6 py-6">
            <ul className="flex flex-col items-center justify-center gap-4 sm:flex-row sm:gap-0">
              {TRUST_POINTS.map(({ icon: Icon, label }, index) => (
                <li
                  key={label}
                  className="flex items-center justify-center sm:flex-1 sm:gap-0"
                >
                  {index > 0 && <VerticalDivider className="mr-8 hidden sm:block" />}
                  <span className="flex flex-1 items-center justify-center gap-2 text-sm text-white/65">
                    <Icon className="size-4 text-brand-green" aria-hidden="true" />
                    {label}
                  </span>
                </li>
              ))}
            </ul>
          </div>

          <GradientDivider />
        </section>

        <HowItWorks />

        <ParallaxDivider />

        <Features />

        <Faq />

        <DarkSection>
          <GradientDivider />
          <div className="mx-auto w-full max-w-6xl px-6 py-20 sm:py-24">
            <div className="relative overflow-hidden rounded-3xl border border-white/12 bg-linear-to-br from-brand-forest/70 via-white/[0.04] to-transparent px-8 py-14 backdrop-blur-sm sm:px-14">
              <div
                aria-hidden="true"
                className="fu-aurora-a pointer-events-none absolute -top-32 -right-24 size-96 rounded-full bg-brand-green/25 blur-3xl"
              />

              <div className="relative flex flex-col items-center gap-6 text-center">
                <h2 className="max-w-xl text-3xl font-semibold tracking-tight text-balance sm:text-4xl">
                  Lost something today?
                </h2>
                <p className="max-w-md text-sm text-pretty text-white/60">
                  Sign in with your campus account and file a report in under a minute.
                </p>

                <Button
                  size="lg"
                  className="group rounded-xl bg-white text-brand-forest hover:bg-white/90"
                  nativeButton={false}
                  render={<Link to={ctaHref} />}
                >
                  {closingLabel}
                  <ArrowRightIcon
                    aria-hidden="true"
                    className="transition-transform duration-200 group-hover:translate-x-0.5"
                  />
                </Button>
              </div>
            </div>
          </div>
        </DarkSection>
      </main>

      <footer className="bg-[oklch(0.15_0.026_148)]">
        <GradientDivider />
        <div className="mx-auto flex w-full max-w-6xl flex-col gap-4 px-6 py-8 sm:flex-row sm:items-center sm:justify-between">
          <span className="flex items-center gap-2.5 text-sm text-white/55">
            <FoundUMark decorative className="size-7 rounded-lg" />
            FoundU - smart campus lost &amp; found.
          </span>

          <div className="flex items-center gap-5 text-xs text-white/40">
            <a
              href="#how-it-works"
              className="transition-colors hover:text-white/75 focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none"
            >
              How it works
            </a>
            <a
              href="#features"
              className="transition-colors hover:text-white/75 focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none"
            >
              Features
            </a>
            <Link
              to="/login"
              className="transition-colors hover:text-white/75 focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none"
            >
              Staff sign in
            </Link>
          </div>
        </div>
      </footer>
    </div>
  )
}

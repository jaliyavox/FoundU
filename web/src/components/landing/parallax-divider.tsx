import type { CSSProperties } from 'react'
import {
  BackpackIcon,
  GlassesIcon,
  HeadphonesIcon,
  KeyRoundIcon,
  SmartphoneIcon,
  UmbrellaIcon,
  WalletIcon,
} from 'lucide-react'
import { GradientDivider } from './bento'
import { useParallax } from '@/hooks/use-parallax'

/**
 * Scroll-driven divider between the two dark sections. A light ground separates them, and
 * four layers moving at different rates give the band depth,
 * so the band visibly separates what is above from what is below rather than just being a rule.
 *
 * Everything here is decorative; the only real content is the line of copy in the middle.
 */

const BACK_ITEMS = [BackpackIcon, KeyRoundIcon, SmartphoneIcon, WalletIcon, HeadphonesIcon, UmbrellaIcon, GlassesIcon]
const FRONT_ITEMS = [WalletIcon, HeadphonesIcon, BackpackIcon, KeyRoundIcon, SmartphoneIcon]

export function ParallaxDivider() {
  const { ref, progress } = useParallax<HTMLDivElement>()

  return (
    <div ref={ref} className="relative isolate overflow-hidden bg-brand-mist">
      <GradientDivider className="via-brand-forest/30" />

      <div className="relative h-72 sm:h-80">
        {/* Layer 1 - light pool, drifts slowest. */}
        <div
          aria-hidden="true"
          className="pointer-events-none absolute inset-0 -z-30"
          style={{
            transform: `translate3d(${progress * -30}px, ${progress * 24}px, 0)`,
            background:
              'radial-gradient(ellipse 55% 120% at 50% 50%, rgba(100,188,109,0.38), transparent 72%)',
          }}
        />

        {/* Layer 2 - repeated wordmark, drifts left. */}
        <div
          aria-hidden="true"
          className="pointer-events-none absolute inset-x-0 top-1/2 -z-20 -translate-y-1/2 select-none"
          style={{ transform: `translate3d(${progress * 120}px, -50%, 0)` }}
        >
          <p className="text-center text-[clamp(3rem,11vw,7rem)] leading-none font-semibold tracking-tight whitespace-nowrap text-brand-forest/[0.07]">
            LOST · FOUND · RETURNED · LOST · FOUND · RETURNED
          </p>
        </div>

        {/* Layer 3 - background icon row, drifts right. */}
        <div
          aria-hidden="true"
          className="pointer-events-none absolute inset-x-0 top-10 -z-10 flex justify-center gap-14 sm:gap-24"
          style={{ transform: `translate3d(${progress * -170}px, 0, 0)` }}
        >
          {BACK_ITEMS.map((Icon, index) => (
            <Icon
              key={index}
              className="size-8 shrink-0 text-brand-forest/20 sm:size-10"
              style={{ transform: `translateY(${(index % 3) * 14}px)` } as CSSProperties}
            />
          ))}
        </div>

        {/* Layer 4 - foreground chips, fastest, so depth is unmistakable. */}
        <div
          aria-hidden="true"
          className="pointer-events-none absolute inset-x-0 bottom-8 flex justify-center gap-10 sm:gap-20"
          style={{ transform: `translate3d(${progress * 300}px, 0, 0)` }}
        >
          {FRONT_ITEMS.map((Icon, index) => (
            <span
              key={index}
              className="flex size-11 shrink-0 items-center justify-center rounded-2xl border border-brand-forest/12 bg-white/70 shadow-sm backdrop-blur-md sm:size-14"
              style={{ transform: `translateY(${(index % 2) * -20}px)` } as CSSProperties}
            >
              <Icon className="size-5 text-brand-forest/70 sm:size-6" />
            </span>
          ))}
        </div>

        {/* The one piece of real content, moving gently against the rest. */}
        <div
          className="absolute inset-0 flex items-center justify-center px-6"
          style={{ transform: `translate3d(0, ${progress * -36}px, 0)` }}
        >
          <p className="max-w-lg text-center text-lg font-medium text-balance text-brand-forest sm:text-2xl">
            Everything handed in gets a second chance to go home.
          </p>
        </div>
      </div>

      <GradientDivider className="via-brand-forest/30" />
    </div>
  )
}

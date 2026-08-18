import { useState } from 'react'
import { Link } from 'react-router-dom'
import { MenuIcon, XIcon } from 'lucide-react'
import { FoundUMark } from '@/components/brand/foundu-logo'
import { Button } from '@/components/ui/button'
import { useScrolled } from '@/hooks/use-scrolled'
import { cn } from '@/lib/utils'

const LINKS = [
  { href: '#how-it-works', label: 'How it works' },
  { href: '#features', label: 'Features' },
  { href: '#faq', label: 'FAQ' },
]

/**
 * Floating pill navigation. Over the dark hero it is a light-on-dark glass bar; once the
 * page scrolls past the hero it swaps to the themed surface so it stays readable on the
 * light sections below.
 */
export function SiteNav({ ctaHref, ctaLabel }: { ctaHref: string; ctaLabel: string }) {
  const scrolled = useScrolled(120)
  const [menuOpen, setMenuOpen] = useState(false)

  return (
    <div className="fixed inset-x-0 top-3 z-50 flex justify-center px-4 sm:top-5">
      <nav
        aria-label="Main"
        className={cn(
          'w-full max-w-3xl rounded-2xl border backdrop-blur-xl transition-all duration-500',
          scrolled
            ? 'border-border bg-background/85 shadow-lg shadow-black/5'
            : 'border-white/15 bg-white/8 shadow-lg shadow-black/20',
        )}
      >
        <div className="flex items-center justify-between gap-3 px-3 py-2 sm:px-4">
          <Link
            to="/"
            className="flex items-center gap-2 rounded-lg px-1 focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none"
          >
            <FoundUMark decorative className="size-8 rounded-lg" />
            <span
              className={cn(
                'text-sm font-semibold tracking-tight transition-colors duration-500',
                scrolled ? 'text-foreground' : 'text-white',
              )}
            >
              FoundU
            </span>
          </Link>

          <ul className="hidden items-center gap-1 md:flex">
            {LINKS.map(({ href, label }) => (
              <li key={href}>
                <a
                  href={href}
                  className={cn(
                    'rounded-lg px-3 py-1.5 text-sm transition-colors duration-200',
                    'focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none',
                    scrolled
                      ? 'text-muted-foreground hover:bg-muted hover:text-foreground'
                      : 'text-white/70 hover:bg-white/10 hover:text-white',
                  )}
                >
                  {label}
                </a>
              </li>
            ))}
          </ul>

          <div className="flex items-center gap-1.5">
            <Button
              size="sm"
              className={cn(
                'rounded-xl transition-colors duration-500',
                !scrolled && 'bg-white text-brand-forest hover:bg-white/90',
              )}
              nativeButton={false}
              render={<Link to={ctaHref} />}
            >
              {ctaLabel}
            </Button>

            <button
              type="button"
              onClick={() => setMenuOpen((open) => !open)}
              aria-expanded={menuOpen}
              aria-controls="site-nav-mobile"
              aria-label={menuOpen ? 'Close menu' : 'Open menu'}
              className={cn(
                'flex size-9 items-center justify-center rounded-xl transition-colors md:hidden',
                'focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none',
                scrolled ? 'text-foreground hover:bg-muted' : 'text-white hover:bg-white/10',
              )}
            >
              {menuOpen ? <XIcon className="size-4.5" /> : <MenuIcon className="size-4.5" />}
            </button>
          </div>
        </div>

        {menuOpen && (
          <ul id="site-nav-mobile" className="flex flex-col gap-0.5 border-t border-border/40 p-2 md:hidden">
            {LINKS.map(({ href, label }) => (
              <li key={href}>
                <a
                  href={href}
                  onClick={() => setMenuOpen(false)}
                  className={cn(
                    'block rounded-lg px-3 py-2 text-sm transition-colors',
                    scrolled ? 'text-foreground hover:bg-muted' : 'text-white/80 hover:bg-white/10',
                  )}
                >
                  {label}
                </a>
              </li>
            ))}
          </ul>
        )}
      </nav>
    </div>
  )
}

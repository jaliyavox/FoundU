import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { ArrowLeftIcon, PlusIcon, SearchIcon, XIcon } from 'lucide-react'
import { GradientDivider } from '@/components/landing/bento'
import { SiteNav } from '@/components/landing/site-nav'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useAuth } from '@/features/auth/use-auth'
import { homeRouteForRole } from '@/routes/role-home'
import { FoundFeed } from './found-feed'

const PUBLIC_LINKS = [
  { href: '/', label: 'Home' },
  { href: '/feed', label: 'Lost board' },
  { href: '/#faq', label: 'FAQ' },
]

/**
 * Everything students have picked up and not yet walked to a desk - the board behind the
 * "Fresh finds" strip on the lost feed. Public to read, like the lost board.
 */
export function FoundBoardPage() {
  const { user } = useAuth()
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')

  function handleSearch(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSearch(searchInput)
  }

  return (
    <div className="flex min-h-svh flex-col bg-[oklch(0.17_0.028_148)] text-white">
      <SiteNav
        ctaHref={user ? homeRouteForRole(user.role) : '/login'}
        ctaLabel={user ? 'Dashboard' : 'Sign in'}
        links={PUBLIC_LINKS}
      />

      <main className="flex-1">
        <section className="relative isolate overflow-hidden pt-32 pb-10 sm:pt-36">
          <div aria-hidden="true" className="pointer-events-none absolute inset-0 -z-10">
            <div className="fu-aurora-a absolute -top-40 left-[10%] size-[34rem] rounded-full bg-brand-forest/60 blur-[130px]" />
            <div className="fu-aurora-b absolute -top-20 right-[5%] size-[26rem] rounded-full bg-brand-green/25 blur-[130px]" />
          </div>

          <div className="mx-auto w-full max-w-5xl px-6">
            <Link to="/feed" className="inline-flex items-center gap-2 text-sm text-white/60 hover:text-white">
              <ArrowLeftIcon className="size-4" aria-hidden="true" />
              Back to the lost board
            </Link>

            <h1 className="pt-5 text-4xl font-semibold tracking-tight text-balance sm:text-5xl">
              What people have found
            </h1>
            <p className="max-w-xl pt-3 text-sm text-pretty text-white/60 sm:text-base">
              Things students picked up and posted before reaching a desk. Recognise yours? Say
              so, and the finder is asked to hand it in.
            </p>

            <div className="flex flex-col gap-3 pt-8 sm:flex-row sm:items-center">
              <form onSubmit={handleSearch} className="flex flex-1 items-center gap-2">
                <div className="relative flex-1">
                  <Label htmlFor="found-search" className="sr-only">
                    Search found items
                  </Label>
                  <SearchIcon
                    className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-white/40"
                    aria-hidden="true"
                  />
                  <Input
                    id="found-search"
                    value={searchInput}
                    onChange={event => setSearchInput(event.target.value)}
                    placeholder="Search by item, colour or place"
                    className="border-white/12 bg-white/[0.06] pr-9 pl-9 text-white placeholder:text-white/35"
                  />
                  {(searchInput || search) && (
                    <button
                      type="button"
                      aria-label="Clear the search"
                      onClick={() => {
                        setSearchInput('')
                        setSearch('')
                      }}
                      className="absolute top-1/2 right-2 flex size-6 -translate-y-1/2 items-center justify-center rounded-full text-white/50 transition-colors hover:bg-white/10 hover:text-white focus-visible:ring-2 focus-visible:ring-white/40 focus-visible:outline-none"
                    >
                      <XIcon className="size-4" aria-hidden="true" />
                    </button>
                  )}
                </div>
                <Button
                  type="submit"
                  variant="outline"
                  className="border-white/20 bg-white/8 text-white hover:bg-white/15 hover:text-white"
                >
                  Search
                </Button>
              </form>

              <Button
                className="rounded-xl bg-white text-brand-forest hover:bg-white/90"
                nativeButton={false}
                render={<Link to={user ? '/found/new' : '/login'} />}
              >
                <PlusIcon aria-hidden="true" />
                {user ? 'Post a found item' : 'Sign in to post'}
              </Button>
            </div>
          </div>
        </section>

        <GradientDivider />

        <section aria-label="Found item posts" className="relative overflow-hidden bg-brand-mist">
          <div aria-hidden="true" className="pointer-events-none absolute inset-0 -z-10">
            <div
              className="absolute inset-0"
              style={{
                background:
                  'radial-gradient(ellipse 70% 80% at 50% 0%, rgba(255,255,255,0.95), transparent 70%)',
              }}
            />
          </div>
          <div className="relative mx-auto w-full max-w-5xl px-6 py-12">
            <FoundFeed search={search} />
          </div>
        </section>
      </main>
    </div>
  )
}

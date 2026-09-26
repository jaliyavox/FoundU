import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { MessageCircleIcon, SendIcon, XIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Textarea } from '@/components/ui/textarea'
import { useAuth } from '@/features/auth/use-auth'
import { cn } from '@/lib/utils'
import { FlameIcon } from '@/features/feed/flame-icon'

/**
 * The floating way in to Ask FoundU, on the pages people land on before they know the app
 * exists - the home page and the public feed.
 *
 * It takes the first sentence and hands it to the real conversation rather than answering
 * here: two chat surfaces that drift apart is a worse outcome than one extra click, and a
 * visitor who is not signed in has to sign in before anything can be searched anyway.
 */
export function AskBubble() {
  const { user } = useAuth()
  const navigate = useNavigate()
  const [open, setOpen] = useState(false)
  const [message, setMessage] = useState('')

  const isStudent = !user || user.role === 'Student'
  // Staff have their own queues; a "what did you lose?" bubble is not for them.
  if (!isStudent) return null

  function submit(event: FormEvent) {
    event.preventDefault()
    const text = message.trim()
    if (!text) return

    // The conversation itself needs an account, so a visitor signs in first and arrives with
    // what they typed still in hand.
    navigate(user ? '/ask-foundu' : '/login', { state: { askFoundU: text } })
  }

  return (
    <>
      {open && (
        <div
          className="fu-appear fixed right-4 bottom-24 z-60 flex w-[min(22rem,calc(100vw-2rem))] flex-col overflow-hidden rounded-2xl bg-white text-neutral-900 shadow-2xl shadow-black/30 ring-1 ring-black/5 sm:right-6"
          role="dialog"
          aria-label="Ask FoundU"
        >
          <div className="flex items-start gap-3 bg-brand-forest p-5 text-white">
            <span className="flex size-10 shrink-0 items-center justify-center rounded-2xl bg-white/10">
              <FlameIcon className="size-6" />
            </span>
            <div className="flex-1">
              <p className="font-medium">Lost something?</p>
              <p className="pt-0.5 text-sm text-white/70">
                Tell me what it is and I will check what has been handed in.
              </p>
            </div>
          </div>

          <form onSubmit={submit} className="flex flex-col gap-3 p-4">
            <Textarea
              rows={3}
              value={message}
              onChange={event => setMessage(event.target.value)}
              maxLength={1000}
              placeholder="I lost a black backpack near the library."
              aria-label="What did you lose?"
              className="border-neutral-900/12 bg-neutral-50 text-neutral-900 placeholder:text-neutral-400"
            />
            <div className="flex items-center justify-between gap-3">
              <p className="text-xs text-neutral-500">
                {user ? 'Opens the full conversation.' : 'Sign in to search - it takes a moment.'}
              </p>
              <Button
                type="submit"
                size="sm"
                disabled={!message.trim()}
                className="bg-brand-forest text-white hover:bg-brand-forest/90"
              >
                <SendIcon aria-hidden="true" />
                Ask
              </Button>
            </div>
          </form>
        </div>
      )}

      <button
        type="button"
        onClick={() => setOpen(value => !value)}
        aria-expanded={open}
        aria-label={open ? 'Close Ask FoundU' : 'Ask FoundU about something you lost'}
        className={cn(
          'fixed right-4 bottom-6 z-60 flex size-14 items-center justify-center rounded-full bg-brand-forest text-white shadow-xl shadow-black/30 transition-transform hover:scale-105 focus-visible:ring-2 focus-visible:ring-brand-green focus-visible:ring-offset-2 focus-visible:outline-none sm:right-6',
        )}
      >
        {open ? (
          <XIcon className="size-6" aria-hidden="true" />
        ) : (
          <MessageCircleIcon className="size-6" aria-hidden="true" />
        )}
      </button>
    </>
  )
}

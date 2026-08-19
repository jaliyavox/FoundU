import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { CheckIcon, Loader2Icon, LogInIcon, SendIcon } from 'lucide-react'
import { useMutation } from '@tanstack/react-query'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { useAuth } from '@/features/auth/use-auth'
import { sendMessage } from './feed-api'
import { ApiError } from '@/lib/api/client'

/**
 * Message the author of a lost report - the sign-in gate the public feed leads to.
 *
 * Nothing here exposes contact details in either direction: the message is stored against
 * the report and the author reads it in-app. It is for "I have handed this in at Security
 * Desk A", not for arranging a handover, which is what the caution beside the card says.
 */
export function MessageAuthor({ reportId, authorName }: { reportId: string; authorName: string }) {
  const { user } = useAuth()
  const [body, setBody] = useState('')
  const [sent, setSent] = useState(false)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({})

  const mutation = useMutation({
    mutationFn: () => sendMessage(reportId, body),
    onSuccess: () => {
      setSent(true)
      setBody('')
      toast.success(`Message sent to ${authorName.split(' ')[0]}.`)
    },
    onError: (error) => {
      if (error instanceof ApiError) {
        setFieldErrors(error.fieldErrors)
        toast.error(error.message)
      } else {
        toast.error('Could not reach the server.')
      }
    },
  })

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setFieldErrors({})
    mutation.mutate()
  }

  /* ------------------------------------------------------------- signed out */
  if (!user) {
    return (
      <div className="flex flex-col gap-3 rounded-xl border border-neutral-900/8 bg-white/70 p-4">
        <p className="text-sm text-neutral-600">
          Sign in to tell {authorName.split(' ')[0]} you have found it.
        </p>
        <Button
          className="bg-brand-forest text-white hover:bg-brand-forest/90"
          nativeButton={false}
          render={<Link to="/login" />}
        >
          <LogInIcon aria-hidden="true" />
          Sign in to message
        </Button>
      </div>
    )
  }

  if (sent) {
    return (
      <p className="flex items-center gap-2 rounded-xl border border-brand-green/30 bg-brand-green/10 p-4 text-sm text-neutral-700">
        <span className="flex size-5 shrink-0 items-center justify-center rounded-full bg-brand-green">
          <CheckIcon className="size-3 text-white" strokeWidth={3.5} aria-hidden="true" />
        </span>
        Sent. {authorName.split(' ')[0]} will see it on their report.
      </p>
    )
  }

  /* -------------------------------------------------------------- signed in */
  return (
    <form onSubmit={handleSubmit} className="flex flex-col gap-2">
      <Label htmlFor="message-body" className="text-sm font-medium text-neutral-900">
        Message {authorName.split(' ')[0]}
      </Label>

      <Textarea
        id="message-body"
        rows={3}
        required
        value={body}
        onChange={(event) => setBody(event.target.value)}
        placeholder="I found this and handed it in at the library desk this morning."
        aria-invalid={Boolean(fieldErrors.Body)}
        aria-describedby={fieldErrors.Body ? 'message-error' : undefined}
        className="border-neutral-900/12 bg-white"
      />

      {fieldErrors.Body && (
        <p id="message-error" className="text-sm text-destructive">
          {fieldErrors.Body.join(' ')}
        </p>
      )}

      <Button
        type="submit"
        disabled={mutation.isPending}
        className="bg-brand-forest text-white hover:bg-brand-forest/90"
      >
        {mutation.isPending ? (
          <Loader2Icon className="animate-spin" aria-hidden="true" />
        ) : (
          <SendIcon aria-hidden="true" />
        )}
        {mutation.isPending ? 'Sending...' : 'Send message'}
      </Button>
    </form>
  )
}

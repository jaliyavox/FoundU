import { useEffect, useState } from 'react'
import { XIcon, ZoomInIcon } from 'lucide-react'
import { assetUrl } from '@/lib/api/client'
import { cn } from '@/lib/utils'

/**
 * The uploaded photo at full size, uncropped.
 *
 * Not a centred modal: on desktop it takes its place in the spotlight row beside the card,
 * so the post it belongs to stays on screen next to it - the same reason the detail panel is
 * a side panel rather than a dialog. On mobile there is no room beside anything, so the
 * caller swaps the card out for this instead of stacking the two.
 *
 * Cards crop with object-cover to keep every tile the same shape, which can hide the very
 * detail that identifies an item - a sticker, a keyring, a scuff. This shows the file as it
 * was uploaded.
 */
export function PhotoViewer({
  photoUrls,
  itemType,
  onClose,
  className,
}: {
  photoUrls: string[]
  itemType: string
  onClose: () => void
  className?: string
}) {
  const [index, setIndex] = useState(0)

  // Reopening on a different post must not land on the previous post's second photo.
  useEffect(() => {
    setIndex(0)
  }, [photoUrls])

  // Escape closes it, the way the dialog it replaces would have.
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose()
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [onClose])

  if (photoUrls.length === 0) return null

  const current = photoUrls[Math.min(index, photoUrls.length - 1)]

  return (
    <div
      role="dialog"
      aria-label={`Photo of the reported ${itemType}`}
      className={cn(
        'fu-spotlight-in pointer-events-auto flex flex-col gap-3 rounded-2xl bg-neutral-950/95 p-3 shadow-2xl shadow-black/50 ring-1 ring-white/15 backdrop-blur-sm',
        className,
      )}
    >
      <div className="relative">
        <img
          src={assetUrl(current)}
          alt={`Photo of the reported ${itemType}`}
          className="max-h-[70svh] w-full rounded-xl object-contain"
        />

        <button
          type="button"
          onClick={onClose}
          aria-label="Close the photo"
          className="absolute top-2 right-2 flex size-8 items-center justify-center rounded-full bg-neutral-950/70 text-white transition-colors hover:bg-neutral-950 focus-visible:ring-3 focus-visible:ring-white/50 focus-visible:outline-none"
        >
          <XIcon className="size-4" aria-hidden="true" />
        </button>
      </div>

      {photoUrls.length > 1 && (
        <div className="flex justify-center gap-2">
          {photoUrls.map((url, position) => (
            <button
              key={url}
              type="button"
              onClick={() => setIndex(position)}
              aria-label={`Show photo ${position + 1}`}
              aria-current={position === index}
              className={cn(
                'size-12 overflow-hidden rounded-lg ring-2 transition-opacity',
                position === index
                  ? 'ring-brand-green'
                  : 'opacity-60 ring-transparent hover:opacity-100',
              )}
            >
              <img src={assetUrl(url)} alt="" className="size-full object-cover" />
            </button>
          ))}
        </div>
      )}
    </div>
  )
}

/** The affordance that opens it: a magnifier over the corner of a cropped photo. */
export function ZoomButton({ onClick, className }: { onClick: () => void; className?: string }) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-label="View the full photo"
      title="View the full photo"
      className={cn(
        'absolute right-3 bottom-3 flex size-9 items-center justify-center rounded-full bg-neutral-950/60 text-white backdrop-blur-sm transition-colors hover:bg-neutral-950/80 focus-visible:ring-3 focus-visible:ring-white/50 focus-visible:outline-none',
        className,
      )}
    >
      <ZoomInIcon className="size-4" aria-hidden="true" />
    </button>
  )
}

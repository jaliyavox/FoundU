import { useEffect, useRef, useState } from 'react'
import { ImagePlusIcon, XIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { PHOTO_RULES } from './reports-api'
import { cn } from '@/lib/utils'

/**
 * Picks up to two images and previews them before the report is submitted.
 *
 * The checks here are for a fast, clear error - the API enforces the same limits, and does
 * it by sniffing the file's leading bytes rather than trusting its extension.
 */
export function PhotoPicker({
  files,
  onChange,
}: {
  files: File[]
  onChange: (files: File[]) => void
}) {
  const inputRef = useRef<HTMLInputElement>(null)
  const [previews, setPreviews] = useState<string[]>([])
  const [error, setError] = useState<string | null>(null)

  // Object URLs are leaked unless they are revoked when the list changes.
  useEffect(() => {
    const urls = files.map((file) => URL.createObjectURL(file))
    setPreviews(urls)
    return () => urls.forEach(URL.revokeObjectURL)
  }, [files])

  function add(selected: FileList | null) {
    if (!selected) return
    setError(null)

    const incoming = Array.from(selected)
    const room = PHOTO_RULES.maxPhotos - files.length

    if (incoming.length > room) {
      setError(`You can add ${PHOTO_RULES.maxPhotos} photos at most.`)
    }

    const accepted: File[] = []

    for (const file of incoming.slice(0, Math.max(0, room))) {
      if (file.size > PHOTO_RULES.maxBytes) {
        setError(`"${file.name}" is larger than ${PHOTO_RULES.maxSizeLabel}.`)
        continue
      }
      accepted.push(file)
    }

    if (accepted.length > 0) onChange([...files, ...accepted])

    // Reset so picking the same file twice still fires a change event.
    if (inputRef.current) inputRef.current.value = ''
  }

  const isFull = files.length >= PHOTO_RULES.maxPhotos

  return (
    <div className="flex flex-col gap-2">
      <Label htmlFor="photos">
        Photos
        <span className="pl-1.5 font-normal text-muted-foreground">(optional)</span>
      </Label>

      <input
        ref={inputRef}
        id="photos"
        type="file"
        multiple
        accept={PHOTO_RULES.accept}
        className="sr-only"
        onChange={(event) => add(event.target.files)}
      />

      <div className="flex flex-wrap gap-3">
        {files.map((file, index) => (
          <figure
            key={`${file.name}-${index}`}
            className="relative size-24 overflow-hidden rounded-xl border bg-muted"
          >
            {previews[index] && (
              <img src={previews[index]} alt="" className="size-full object-cover" />
            )}
            <button
              type="button"
              onClick={() => onChange(files.filter((_, i) => i !== index))}
              aria-label={`Remove ${file.name}`}
              className="absolute top-1 right-1 flex size-6 items-center justify-center rounded-full bg-black/60 text-white transition-colors hover:bg-black/80 focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
            >
              <XIcon className="size-3.5" />
            </button>
          </figure>
        ))}

        {!isFull && (
          <button
            type="button"
            onClick={() => inputRef.current?.click()}
            className={cn(
              'flex size-24 flex-col items-center justify-center gap-1 rounded-xl border border-dashed text-muted-foreground transition-colors',
              'hover:border-brand-green hover:text-brand-green focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none',
            )}
          >
            <ImagePlusIcon className="size-5" aria-hidden="true" />
            <span className="text-xs">Add photo</span>
          </button>
        )}
      </div>

      <p className={cn('text-xs', error ? 'text-destructive' : 'text-muted-foreground')} role={error ? 'alert' : undefined}>
        {error ?? `Up to ${PHOTO_RULES.maxPhotos} images, ${PHOTO_RULES.maxSizeLabel} each. JPEG, PNG or WebP.`}
      </p>

      {files.length > 0 && (
        <Button
          type="button"
          variant="ghost"
          size="sm"
          className="w-fit"
          onClick={() => onChange([])}
        >
          Remove all
        </Button>
      )}
    </div>
  )
}

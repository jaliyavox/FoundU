import { assetUrl } from '@/lib/api/client'
import { cn } from '@/lib/utils'
import { ItemIllustration } from './item-illustration'

/**
 * A post's picture: the first uploaded photo when there is one, the monoline illustration
 * when there is not.
 *
 * Upload URLs are stored host-relative ("/uploads/lost/xxx.jpg") because the API serves them
 * from its own wwwroot - so they have to be resolved against the API origin, not the page's.
 */
export function ItemMedia({
  photoUrl,
  itemType,
  category,
  className,
  illustrationClassName,
}: {
  photoUrl?: string
  itemType: string
  category: string
  /** Applied to the photo. */
  className?: string
  /** Applied to the fallback illustration, which is sized differently from a photo. */
  illustrationClassName?: string
}) {
  if (photoUrl) {
    return (
      <img
        src={assetUrl(photoUrl)}
        // Decorative: the item type and description next to it already name the thing, so a
        // generated alt would only repeat them to a screen reader.
        alt=""
        loading="lazy"
        className={cn('size-full object-cover', className)}
      />
    )
  }

  return (
    <ItemIllustration itemType={itemType} category={category} className={illustrationClassName} />
  )
}

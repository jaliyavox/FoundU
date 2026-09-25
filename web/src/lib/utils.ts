import { clsx, type ClassValue } from "clsx"
import { twMerge } from "tailwind-merge"

/**
 * Merges Tailwind classes so later ones win over earlier conflicting ones,
 * e.g. cn("px-2", isWide && "px-8") resolves to "px-8" rather than both.
 */
export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

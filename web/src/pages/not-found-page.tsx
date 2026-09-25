import { Link } from 'react-router-dom'
import { Button } from '@/components/ui/button'

export function NotFoundPage() {
  return (
    <main className="flex min-h-svh flex-col items-center justify-center gap-3 px-4 text-center">
      <p className="text-sm font-medium text-muted-foreground">404</p>
      <h1 className="text-2xl font-semibold tracking-tight">Page not found</h1>
      {/* Base UI composes via `render`, not Radix's `asChild`. */}
      <Button variant="outline" nativeButton={false} render={<Link to="/" />}>
        Back to FoundU
      </Button>
    </main>
  )
}

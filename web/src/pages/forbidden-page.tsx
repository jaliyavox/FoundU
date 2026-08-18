import { Link } from 'react-router-dom'
import { Button } from '@/components/ui/button'

export function ForbiddenPage() {
  return (
    <section className="flex flex-col items-start gap-3">
      <h1 className="text-2xl font-semibold tracking-tight">Access denied</h1>
      <p className="text-sm text-muted-foreground">
        Your account does not have permission to view that page.
      </p>
      {/* Base UI composes via `render`, not Radix's `asChild`. */}
      <Button variant="outline" nativeButton={false} render={<Link to="/" />}>
        Back to your dashboard
      </Button>
    </section>
  )
}

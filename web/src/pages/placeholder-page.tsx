import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'

interface PlaceholderPageProps {
  title: string
  description: string
  /** Which build step fills this screen in - keeps the shell honest about what is stubbed. */
  nextStep: string
}

export function PlaceholderPage({ title, description, nextStep }: PlaceholderPageProps) {
  return (
    <section className="flex flex-col gap-4">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">{title}</h1>
        <p className="text-sm text-muted-foreground">{description}</p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Not built yet</CardTitle>
          <CardDescription>{nextStep}</CardDescription>
        </CardHeader>
        <CardContent className="text-sm text-muted-foreground">
          The shell, routing and role guards are in place - this screen is waiting on its API.
        </CardContent>
      </Card>
    </section>
  )
}

import type { FullConfig } from '@playwright/test'
import process from 'node:process'

export default async function globalSetup(_config: FullConfig) {
  const apiBase = process.env.VITE_API_BASE_URL ?? 'http://localhost:5292'

  try {
    const response = await fetch(new URL('/api/health', apiBase), {
      signal: AbortSignal.timeout(3_000),
    })
    if (!response.ok) throw new Error(`health endpoint returned ${response.status}`)
  } catch (error) {
    const detail = error instanceof Error ? error.message : String(error)
    throw new Error(
      `FoundU E2E requires the ASP.NET API at ${apiBase}. Start the API and seed its development data before running npm run test:e2e. (${detail})`,
    )
  }
}
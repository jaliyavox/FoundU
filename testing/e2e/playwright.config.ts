import { defineConfig } from '@playwright/test'

/**
 * End-to-end tests against the running stack: web (Vite), API (ASP.NET), AI service (FastAPI)
 * and PostgreSQL. Start them as the README describes, then `npm test` here.
 *
 * WEB_URL / API_URL point elsewhere (for example the Render deployment) when set.
 */
export default defineConfig({
  testDir: './tests',
  timeout: 90_000,
  expect: { timeout: 10_000 },
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: [
    ['list'],
    ['html', { outputFolder: '../reports/e2e/html', open: 'never' }],
    ['junit', { outputFile: '../reports/e2e/junit.xml' }],
    ['json', { outputFile: '../reports/e2e/results.json' }],
  ],
  outputDir: '../reports/e2e/artifacts',
  use: {
    baseURL: process.env.WEB_URL ?? 'http://127.0.0.1:5173',
    channel: 'chrome',
    viewport: { width: 1280, height: 900 },
    screenshot: 'on',
    trace: 'retain-on-failure',
  },
})

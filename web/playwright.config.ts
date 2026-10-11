import { defineConfig, devices } from '@playwright/test'
import process from 'node:process'

const baseURL = process.env.FOUNDU_E2E_BASE_URL ?? 'http://127.0.0.1:5174'

export default defineConfig({
  testDir: './e2e',
  globalSetup: './e2e/global-setup.ts',
  fullyParallel: false,
  workers: 1,
  reporter: process.env.CI ? 'dot' : 'list',
  timeout: 45_000,
  expect: { timeout: 8_000 },
  use: {
    ...devices['Desktop Chrome'],
    baseURL,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: process.env.FOUNDU_E2E_BASE_URL
    ? undefined
    : {
        command: 'npm run dev -- --host 127.0.0.1 --port 5174 --strictPort',
        url: baseURL,
        reuseExistingServer: !process.env.CI,
        timeout: 120_000,
        env: {
          VITE_API_BASE_URL: process.env.VITE_API_BASE_URL ?? 'http://localhost:5292',
        },
    },
})
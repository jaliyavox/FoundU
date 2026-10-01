import path from 'node:path'
// vitest/config re-exports Vite's defineConfig with the `test` block typed.
import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': path.resolve(import.meta.dirname, './src'),
    },
  },
  test: {
    // React web tests live in a dedicated folder and can use DOM helpers for component-level checks.
    environment: 'jsdom',
    setupFiles: ['./tests/setup.ts'],
    include: ['tests/**/*.test.ts'],
    env: { VITE_API_BASE_URL: 'http://api.test' },
  },
})

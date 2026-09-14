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
    // The tests cover the logic that decides what a screen says - stage of a report, where a
    // notification links, how a time reads - none of which needs a DOM.
    environment: 'node',
    include: ['src/**/*.test.ts'],
    env: { VITE_API_BASE_URL: 'http://api.test' },
  },
})

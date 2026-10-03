import { randomBytes } from 'node:crypto'
import type { Page } from '@playwright/test'

/** Test data setup goes straight to the API; what a test is about goes through the UI. */
export const API = process.env.API_URL ?? 'http://localhost:5292'
export const STAFF = { email: 'priya@foundu.test', password: process.env.FOUNDU_DEMO_PASSWORD ?? 'Demo!Pass2026' }
export const STUDENT = { email: 'amara@foundu.test', password: process.env.FOUNDU_DEMO_PASSWORD ?? 'Demo!Pass2026' }

export const run = () => randomBytes(3).toString('hex')

export async function call<T = any>(method: string, path: string, token?: string, body?: unknown, expect = [200, 201, 204]): Promise<T> {
  const res = await fetch(API + path, {
    method,
    headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  const text = await res.text()
  if (!expect.includes(res.status)) throw new Error(`${method} ${path} -> ${res.status}: ${text.slice(0, 300)}`)
  return (text ? JSON.parse(text) : null) as T
}

export type Session = { accessToken: string; refreshToken: string }
export const login = (email: string, password: string) => call<Session>('POST', '/api/auth/login', undefined, { email, password })

export async function registerStudent(label: string) {
  const id = run()
  const account = {
    fullName: `E2E ${label} ${id}`,
    email: `e2e-${label.toLowerCase()}-${id}@foundu.test`,
    password: `E2e!${randomBytes(6).toString('hex')}aA9`,
    studentNumber: `IT26${Math.floor(100000 + Math.random() * 899999)}`,
  }
  const session = await call<Session>('POST', '/api/auth/register', undefined, account)
  return { ...account, ...session }
}

/** Puts a session in the browser the way the login page does, then opens `path`. */
export async function signInAs(page: Page, session: Session, path: string) {
  await page.goto('/login')
  await page.evaluate(({ a, r }) => {
    localStorage.clear()
    localStorage.setItem('foundu.accessToken', a)
    localStorage.setItem('foundu.refreshToken', r)
    localStorage.setItem('foundu.dashboardTheme', 'light')
  }, { a: session.accessToken, r: session.refreshToken })
  await page.goto(path)
}

export async function referenceData(token: string) {
  const categories = await call<any[]>('GET', '/api/reference/categories', token)
  const category = categories.find((c) => c.itemTypes.length > 0)
  const location = (await call<any[]>('GET', '/api/reference/locations', token))[0]
  const storage = (await call<any[]>('GET', '/api/reference/storage-locations', token))[0]
  return { category, itemType: category.itemTypes[0], location, storage }
}

const iso = (minutesAgo: number) => new Date(Date.now() - minutesAgo * 60_000).toISOString()

export async function createLostReport(token: string, ref: Awaited<ReturnType<typeof referenceData>>, description: string, colour = 'Purple') {
  return call('POST', '/api/lost-reports', token, {
    categoryId: ref.category.id,
    itemTypeId: ref.itemType.id,
    primaryColor: colour,
    lastSeenLocationId: ref.location.id,
    description,
    estimatedLostFromAt: iso(180),
    estimatedLostToAt: iso(120),
  })
}

export async function logFoundItem(token: string, ref: Awaited<ReturnType<typeof referenceData>>, description: string, hidden: string, colour = 'Purple') {
  return call('POST', '/api/found-reports', token, {
    categoryId: ref.category.id,
    itemTypeId: ref.itemType.id,
    primaryColor: colour,
    foundLocationId: ref.location.id,
    storageLocationId: ref.storage.id,
    generalDescription: description,
    privateVerificationDetails: hidden,
    foundAt: iso(60),
  })
}

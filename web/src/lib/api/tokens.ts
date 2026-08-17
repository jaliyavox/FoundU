import type { AuthResponse, User } from './types'

const ACCESS_TOKEN_KEY = 'foundu.accessToken'
const REFRESH_TOKEN_KEY = 'foundu.refreshToken'
const USER_KEY = 'foundu.user'

/**
 * The only module that touches persistent storage, so swapping localStorage for something
 * else later is a single-file change.
 *
 * Trade-off: localStorage survives a page reload but is readable by any script on the page,
 * so an XSS bug becomes account theft. The access token is short-lived (15 minutes) to limit
 * the blast radius. The alternative - an httpOnly cookie - is not available because the API
 * returns tokens in the response body.
 */
export const tokenStore = {
  getAccessToken: () => localStorage.getItem(ACCESS_TOKEN_KEY),

  getRefreshToken: () => localStorage.getItem(REFRESH_TOKEN_KEY),

  /** The cached user, so a reload renders immediately instead of flashing the login page. */
  getUser(): User | null {
    const raw = localStorage.getItem(USER_KEY)
    if (!raw) return null

    try {
      return JSON.parse(raw) as User
    } catch {
      // Corrupt entry (hand-edited, or written by an older version) - treat as logged out.
      return null
    }
  },

  save(auth: AuthResponse) {
    localStorage.setItem(ACCESS_TOKEN_KEY, auth.accessToken)
    localStorage.setItem(REFRESH_TOKEN_KEY, auth.refreshToken)
    localStorage.setItem(USER_KEY, JSON.stringify(auth.user))
  },

  clear() {
    localStorage.removeItem(ACCESS_TOKEN_KEY)
    localStorage.removeItem(REFRESH_TOKEN_KEY)
    localStorage.removeItem(USER_KEY)
  },
}

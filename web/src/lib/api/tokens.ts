import type { AuthResponse } from './types'

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

  save(auth: AuthResponse) {
    localStorage.setItem(ACCESS_TOKEN_KEY, auth.accessToken)
    localStorage.setItem(REFRESH_TOKEN_KEY, auth.refreshToken)
    // Identity is validated through /me on reload; discard the old cached-user format.
    localStorage.removeItem(USER_KEY)
  },

  clear() {
    localStorage.removeItem(ACCESS_TOKEN_KEY)
    localStorage.removeItem(REFRESH_TOKEN_KEY)
    localStorage.removeItem(USER_KEY)
  },
}

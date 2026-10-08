// NF-PERF-03 Spike on sign-in: 9 o'clock, a lecture ends and 30 students sign in within seconds.
// Sign-in hashes the password (PBKDF2), so it is the most CPU-expensive request the API serves.
// Pass criteria: p95 < 1500 ms, nothing fails.
import http from 'k6/http'
import { check } from 'k6'
import { API, summary } from './common.js'

const accounts = ['amara@foundu.test', 'dev@foundu.test', 'nadia@foundu.test', 'kasun@foundu.test']
const PASSWORD = __ENV.FOUNDU_DEMO_PASSWORD || 'Demo!Pass2026'

export const options = {
  scenarios: {
    spike: { executor: 'per-vu-iterations', vus: 30, iterations: 1, maxDuration: '30s' },
  },
  thresholds: { http_req_failed: ['rate==0'], 'http_req_duration{name:login}': ['p(95)<1500'] },
}

export default function () {
  const email = accounts[__VU % accounts.length]
  const res = http.post(`${API}/api/auth/login`, JSON.stringify({ email, password: PASSWORD }),
    { headers: { 'Content-Type': 'application/json' }, tags: { name: 'login' } })
  check(res, { 'signed in': (r) => r.status === 200 && !!r.json('accessToken') })
}

export const handleSummary = summary('login-spike')

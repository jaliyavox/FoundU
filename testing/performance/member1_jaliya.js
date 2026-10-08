// NF-PERF-M1 Member 1 (Jaliya): administration and support. Admins page through user management,
// staff work the support queue, and people keep signing in, ramping to 20 users over a minute.
// Pass criteria: p95 < 500 ms, under 1% failed requests.
// Needs FOUNDU_ADMIN_PASSWORD (the local admin's password; never committed).
import http from 'k6/http'
import { check, sleep } from 'k6'
import { API, auth, signIn, summary } from './common.js'

export const options = {
  scenarios: {
    desk: {
      executor: 'ramping-vus',
      exec: 'desk',
      stages: [
        { duration: '20s', target: 20 },
        { duration: '30s', target: 20 },
        { duration: '10s', target: 0 },
      ],
    },
    // Password hashing is deliberately slow, so sign-ins come at a steady, realistic rate.
    signIns: { executor: 'constant-arrival-rate', exec: 'signInFlow', rate: 2, timeUnit: '1s', duration: '1m', preAllocatedVUs: 4 },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<500'],
    'http_req_duration{name:admin users}': ['p(95)<500'],
    'http_req_duration{name:support queue}': ['p(95)<500'],
    'http_req_duration{name:sign in}': ['p(95)<500'],
    checks: ['rate>0.99'],
    'http_reqs{name:admin users}': ['count>0'],
    'http_reqs{name:support queue}': ['count>0'],
    'http_reqs{name:sign in}': ['count>0'],
  },
}

export function setup() {
  if (!__ENV.FOUNDU_ADMIN_PASSWORD) throw new Error('set FOUNDU_ADMIN_PASSWORD')
  return { admin: signIn('admin@foundu.com', __ENV.FOUNDU_ADMIN_PASSWORD), staff: signIn('priya@foundu.test') }
}

export function desk(t) {
  const users = http.get(`${API}/api/admin/users?page=1&pageSize=20`, auth(t.admin, 'admin users'))
  check(users, { 'admin users 200': (r) => r.status === 200 })
  const queue = http.get(`${API}/api/admin/support/tickets?page=1&pageSize=20`, auth(t.staff, 'support queue'))
  check(queue, { 'support queue 200': (r) => r.status === 200 })
  sleep(1 + Math.random())
}

export function signInFlow() {
  const res = http.post(`${API}/api/auth/login`,
    JSON.stringify({ email: 'amara@foundu.test', password: __ENV.FOUNDU_DEMO_PASSWORD || 'Demo!Pass2026' }),
    { headers: { 'Content-Type': 'application/json' }, tags: { name: 'sign in' } })
  check(res, { 'sign in 200': (r) => r.status === 200 })
}

export const handleSummary = summary('member1')

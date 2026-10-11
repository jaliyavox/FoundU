// NF-PERF-M4 Member 4 (Braveena): claims. Students check their claims and open one while staff
// work the claims queue, ramping to 30 users over a minute.
// Pass criteria: p95 < 500 ms, under 1% failed requests.
import http from 'k6/http'
import { check, sleep } from 'k6'
import { API, auth, signIn, summary } from './common.js'

export const options = {
  scenarios: {
    mixed: {
      executor: 'ramping-vus',
      stages: [
        { duration: '20s', target: 30 },
        { duration: '30s', target: 30 },
        { duration: '10s', target: 0 },
      ],
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<500'],
    'http_req_duration{name:my claims}': ['p(95)<500'],
    'http_req_duration{name:claims queue}': ['p(95)<500'],
    'http_req_duration{name:claim detail}': ['p(95)<500'],
    checks: ['rate>0.99'],
    'http_reqs{name:my claims}': ['count>0'],
    'http_reqs{name:claims queue}': ['count>0'],
    'http_reqs{name:claim detail}': ['count>0'],
  },
}

export function setup() {
  const student = signIn('amara@foundu.test')
  const staff = signIn('priya@foundu.test')
  const queue = http.get(`${API}/api/claims?pageSize=1`, auth(staff, 'setup'))
  const claimId = queue.json('items.0.id')
  if (!claimId) throw new Error('no claim in the queue to open - run the E2E suite or seed data first')
  return { student, staff, claimId }
}

export default function (t) {
  const mine = http.get(`${API}/api/claims/mine?pageSize=10`, auth(t.student, 'my claims'))
  check(mine, { 'my claims 200': (r) => r.status === 200 })
  const queue = http.get(`${API}/api/claims?pageSize=20`, auth(t.staff, 'claims queue'))
  check(queue, { 'claims queue 200': (r) => r.status === 200 })
  const detail = http.get(`${API}/api/claims/${t.claimId}`, auth(t.staff, 'claim detail'))
  check(detail, { 'claim detail 200': (r) => r.status === 200 })
  sleep(1 + Math.random() * 2)
}

export const handleSummary = summary('member4')

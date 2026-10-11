// NF-PERF-M3 Member 3 (Uthpala): found items and the desk. Students browse the Found board while
// desk staff page through what is in storage and look up hand-in codes, ramping to 30 users.
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
    'http_req_duration{name:found board}': ['p(95)<500'],
    'http_req_duration{name:desk storage list}': ['p(95)<500'],
    'http_req_duration{name:desk code lookup}': ['p(95)<500'],
    checks: ['rate>0.99'],
    'http_reqs{name:found board}': ['count>0'],
    'http_reqs{name:desk storage list}': ['count>0'],
    'http_reqs{name:desk code lookup}': ['count>0'],
  },
}

export function setup() {
  const student = signIn('amara@foundu.test')
  const staff = signIn('priya@foundu.test')
  // A real hand-in code, so the lookup does the work it does at the desk.
  const mine = http.get(`${API}/api/lost-reports/my-reports?pageSize=1`, auth(student, 'setup'))
  const code = mine.json('items.0.handInCode') || '123456'
  return { student, staff, code }
}

export default function (t) {
  const board = http.get(`${API}/api/found-posts/feed?pageSize=12`, auth(t.student, 'found board'))
  check(board, { 'found board 200': (r) => r.status === 200 })
  const storage = http.get(`${API}/api/found-reports?status=InStorage&pageSize=20`, auth(t.staff, 'desk storage list'))
  check(storage, { 'storage 200': (r) => r.status === 200 })
  const lookup = http.get(`${API}/api/desk/codes/${t.code}`, auth(t.staff, 'desk code lookup'))
  check(lookup, { 'code lookup 200': (r) => r.status === 200 })
  sleep(1 + Math.random() * 2)
}

export const handleSummary = summary('member3')

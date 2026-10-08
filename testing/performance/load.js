// NF-PERF-01 Load test: a busy lunchtime on campus. Up to 50 concurrent users for about 2 minutes,
// browsing the public feeds, searching, checking their own reports and polling notifications,
// while desk staff page through the storage list.
// Pass criteria: p95 < 500 ms on every endpoint, under 1% failed requests.
import http from 'k6/http'
import { check, sleep } from 'k6'
import { API, auth, signIn, summary } from './common.js'

export const options = {
  scenarios: {
    students: {
      executor: 'ramping-vus',
      exec: 'student',
      stages: [
        { duration: '20s', target: 20 },
        { duration: '60s', target: 50 },
        { duration: '30s', target: 50 },
        { duration: '10s', target: 0 },
      ],
    },
    desk: { executor: 'constant-vus', exec: 'staff', vus: 5, duration: '2m' },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<500'],
    'http_req_duration{name:lost feed}': ['p(95)<500'],
    'http_req_duration{name:found board}': ['p(95)<500'],
    'http_req_duration{name:feed search}': ['p(95)<500'],
    'http_req_duration{name:my reports}': ['p(95)<500'],
    'http_req_duration{name:unread count}': ['p(95)<300'],
    'http_req_duration{name:desk storage list}': ['p(95)<500'],
    checks: ['rate>0.99'],
    'http_reqs{name:lost feed}': ['count>0'],
    'http_reqs{name:found board}': ['count>0'],
    'http_reqs{name:feed search}': ['count>0'],
    'http_reqs{name:my reports}': ['count>0'],
    'http_reqs{name:unread count}': ['count>0'],
    'http_reqs{name:desk storage list}': ['count>0'],
  },
}

export function setup() {
  return { student: signIn('amara@foundu.test'), staff: signIn('priya@foundu.test') }
}

const searches = ['wallet', 'phone', 'keys', 'bag', 'black', 'library']

export function student(t) {
  const r1 = http.get(`${API}/api/lost-reports/feed?page=1&pageSize=12`, { tags: { name: 'lost feed' } })
  check(r1, { 'lost feed 200': (r) => r.status === 200 })
  const r2 = http.get(`${API}/api/found-posts/feed?pageSize=12`, { tags: { name: 'found board' } })
  check(r2, { 'found board 200': (r) => r.status === 200 })
  const q = searches[Math.floor(Math.random() * searches.length)]
  const r3 = http.get(`${API}/api/lost-reports/feed?search=${q}&pageSize=12`, { tags: { name: 'feed search' } })
  check(r3, { 'search 200': (r) => r.status === 200 })
  const r4 = http.get(`${API}/api/lost-reports/my-reports?pageSize=10`, auth(t.student, 'my reports'))
  check(r4, { 'my reports 200': (r) => r.status === 200 })
  const r5 = http.get(`${API}/api/notifications/unread-count`, auth(t.student, 'unread count'))
  check(r5, { 'unread 200': (r) => r.status === 200 })
  sleep(1 + Math.random() * 2) // think time
}

export function staff(t) {
  const r = http.get(`${API}/api/found-reports?status=InStorage&pageSize=20`, auth(t.staff, 'desk storage list'))
  check(r, { 'storage 200': (res) => res.status === 200 })
  sleep(2)
}

export const handleSummary = summary('load')

// NF-PERF-M2 Member 2 (Ranasinghe): lost item reporting and tracking. Students browse the lost
// feed, search it and check their own reports, ramping to 50 users over a minute.
// Pass criteria: p95 < 500 ms, under 1% failed requests.
import http from 'k6/http'
import { check, sleep } from 'k6'
import { API, auth, signIn, summary } from './common.js'

export const options = {
  scenarios: {
    students: {
      executor: 'ramping-vus',
      stages: [
        { duration: '20s', target: 50 },
        { duration: '30s', target: 50 },
        { duration: '10s', target: 0 },
      ],
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<500'],
    'http_req_duration{name:lost feed}': ['p(95)<500'],
    'http_req_duration{name:feed search}': ['p(95)<500'],
    'http_req_duration{name:my reports}': ['p(95)<500'],
    checks: ['rate>0.99'],
    'http_reqs{name:lost feed}': ['count>0'],
    'http_reqs{name:feed search}': ['count>0'],
    'http_reqs{name:my reports}': ['count>0'],
  },
}

export function setup() {
  return { student: signIn('amara@foundu.test') }
}

const searches = ['wallet', 'phone', 'keys', 'bag', 'black', 'library']

export default function (t) {
  const feed = http.get(`${API}/api/lost-reports/feed?page=1&pageSize=12`, { tags: { name: 'lost feed' } })
  check(feed, { 'lost feed 200': (r) => r.status === 200 })
  const q = searches[Math.floor(Math.random() * searches.length)]
  const search = http.get(`${API}/api/lost-reports/feed?search=${q}&pageSize=12`, { tags: { name: 'feed search' } })
  check(search, { 'feed search 200': (r) => r.status === 200 })
  const mine = http.get(`${API}/api/lost-reports/my-reports?pageSize=10`, auth(t.student, 'my reports'))
  check(mine, { 'my reports 200': (r) => r.status === 200 })
  sleep(1 + Math.random() * 2)
}

export const handleSummary = summary('member2')

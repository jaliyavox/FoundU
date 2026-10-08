// NF-PERF-02 Stress test: keep adding users past the expected peak (50) up to 300, with no think
// time, to find where response times degrade and whether the API fails safely (no 5xx, recovers).
// Thresholds are deliberately loose: the point is the shape of the curve and that nothing crashes.
import http from 'k6/http'
import { check } from 'k6'
import { API, auth, signIn, summary } from './common.js'

export const options = {
  stages: [
    { duration: '30s', target: 50 },
    { duration: '30s', target: 100 },
    { duration: '30s', target: 200 },
    { duration: '30s', target: 300 },
    { duration: '30s', target: 0 }, // recovery
  ],
  thresholds: {
    'http_req_failed': ['rate<0.05'],
    'checks{kind:no5xx}': ['rate==1'],
    'http_req_duration': ['p(95)<2000'],
  },
}

export function setup() {
  return { student: signIn('amara@foundu.test') }
}

export default function (t) {
  const feed = http.get(`${API}/api/lost-reports/feed?page=1&pageSize=12`, { tags: { name: 'lost feed' } })
  const mine = http.get(`${API}/api/lost-reports/my-reports?pageSize=10`, auth(t.student, 'my reports'))
  for (const r of [feed, mine]) check(r, { 'no 5xx': (res) => res.status < 500 }, { kind: 'no5xx' })
}

export const handleSummary = summary('stress')

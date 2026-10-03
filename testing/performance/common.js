// Shared settings for the k6 scripts. API_URL points the run at another deployment (e.g. Render).
import http from 'k6/http'

export const API = __ENV.API_URL || 'http://localhost:5292'
const PASSWORD = __ENV.FOUNDU_DEMO_PASSWORD || 'Demo!Pass2026'
const json = { headers: { 'Content-Type': 'application/json' } }

/** Signs in once per run (in setup), so the load is on the endpoints and not on password hashing. */
export function signIn(email) {
  const res = http.post(`${API}/api/auth/login`, JSON.stringify({ email, password: PASSWORD }), json)
  if (res.status !== 200) throw new Error(`login ${email} -> ${res.status}`)
  return res.json('accessToken')
}

export const auth = (token, tag) => ({ headers: { Authorization: `Bearer ${token}` }, tags: { name: tag } })

export function summary(name) {
  return (data) => ({
    [`../reports/performance/${name}-summary.json`]: JSON.stringify(data, null, 2),
    stdout: textSummary(data),
  })
}

// A compact console summary: one line per request group, with the figures the report uses.
function textSummary(data) {
  const m = data.metrics
  const lines = [`\n${'endpoint'.padEnd(34)} ${'reqs'.padStart(7)} ${'avg ms'.padStart(8)} ${'p95 ms'.padStart(8)} ${'max ms'.padStart(8)}`]
  for (const [key, v] of Object.entries(m)) {
    const g = key.match(/^http_req_duration\{name:(.+)\}$/)
    if (!g || !v.values) continue
    const count = m[`http_reqs{name:${g[1]}}`]?.values.count ?? ''
    lines.push(`${g[1].padEnd(34)} ${String(count).padStart(7)} ${v.values.avg.toFixed(1).padStart(8)} ${v.values['p(95)'].toFixed(1).padStart(8)} ${v.values.max.toFixed(1).padStart(8)}`)
  }
  const d = m.http_req_duration.values
  lines.push(`\nall requests: ${m.http_reqs.values.count} (${m.http_reqs.values.rate.toFixed(1)}/s), p95 ${d['p(95)'].toFixed(1)} ms, failed ${(m.http_req_failed.values.rate * 100).toFixed(2)}%`)
  for (const [name, t] of Object.entries(data.metrics)) {
    if (!t.thresholds) continue
    for (const [rule, r] of Object.entries(t.thresholds)) lines.push(`threshold ${name} ${rule}: ${r.ok ? 'PASS' : 'FAIL'}`)
  }
  return lines.join('\n') + '\n'
}

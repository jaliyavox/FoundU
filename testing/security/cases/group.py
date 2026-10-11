"""Group: CORS, the AI service key, token forgery

Group-owned checks that protect every component.
"""

from .common import case, no_leak, status

CASES = [
    case("SEC-02 garbage bearer token is refused", "GET", "/api/auth/me", headers={"Authorization": "Bearer not.a.jwt"}, tests=status(401)),
    case("SEC-03 token with a forged role claim (signature no longer matches) is refused", "GET", "/api/admin/users",
         headers={"Authorization": "Bearer {{forged}}"},
         pre="""const [h, p, s] = pm.environment.get('studentA').split('.');
    const b64 = x => Buffer.from(x, 'base64').toString();
    const enc = x => Buffer.from(x).toString('base64').replace(/=+$/, '').replace(/\\+/g, '-').replace(/\\//g, '_');
    const payload = JSON.parse(b64(p.replace(/-/g, '+').replace(/_/g, '/')));
    for (const k of Object.keys(payload)) if (/role$/i.test(k)) payload[k] = 'Admin';
    pm.environment.set('forged', [h, enc(JSON.stringify(payload)), s].join('.'));""",
         tests=status(401)),
    case("SEC-04 unsigned token (alg: none) is refused", "GET", "/api/auth/me",
         headers={"Authorization": "Bearer {{unsigned}}"},
         pre="""const [, p] = pm.environment.get('studentA').split('.');
    const header = Buffer.from(JSON.stringify({ alg: 'none', typ: 'JWT' })).toString('base64').replace(/=+$/, '');
    pm.environment.set('unsigned', `${header}.${p}.`);""",
         tests=status(401)),
    case("SEC-25 CORS does not allow an arbitrary origin", "OPTIONS", "/api/lost-reports/feed",
         headers={"Origin": "https://evil.example", "Access-Control-Request-Method": "GET"},
         tests="pm.test('no CORS grant for evil.example', () => pm.expect(pm.response.headers.get('Access-Control-Allow-Origin')).to.not.equal('https://evil.example'));"),
    case("SEC-26 the AI service refuses calls without the service key", "POST", "{{ai}}/agents/run",
         body={"agent": "matching"}, tests=status(401, 403)),
]

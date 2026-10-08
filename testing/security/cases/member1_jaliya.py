"""Member 1 - Jaliya H. A. W (IT24101976)

Authentication, account email, admin user management, support tickets, response headers and NUL bytes.
"""

from .common import case, no_leak, status

CASES = [
    case("SEC-01 anonymous request to a protected endpoint is refused", "GET", "/api/lost-reports/my-reports", tests=status(401)),
    case("SEC-05 wrong password and unknown email get the same answer (no account enumeration)", "POST", "/api/auth/login",
         body={"email": "nobody-here@foundu.test", "password": "Wrong-pass1"},
         tests=status(401) + """
    const strip = t => { const b = JSON.parse(t); delete b.traceId; return JSON.stringify(b); };
    const unknown = strip(pm.response.text());
    pm.sendRequest({ url: pm.environment.get('api') + '/api/auth/login', method: 'POST', header: { 'Content-Type': 'application/json' },
      body: { mode: 'raw', raw: JSON.stringify({ email: 'kasun@foundu.test', password: 'Wrong-pass1' }) } }, (e, r) => {
      pm.test('known email with wrong password: same status', () => pm.expect(r.code).to.equal(401));
      pm.test('same body apart from the trace id, so an attacker cannot tell which emails exist', () => pm.expect(strip(r.text())).to.equal(unknown));
    });"""),
    case("SEC-06 forgot-password answers the same for unknown emails", "POST", "/api/auth/forgot-password",
         body={"email": "nobody-at-all@foundu.test"}, tests=status(202)),
    case("SEC-07 a refresh token cannot be used twice (rotation)", "POST", "/api/auth/refresh",
         raw='{"refreshToken":"{{refreshA}}"}',
         tests=status(200) + """
    pm.environment.set('studentA', pm.response.json().accessToken);
    pm.sendRequest({ url: pm.environment.get('api') + '/api/auth/refresh', method: 'POST', header: { 'Content-Type': 'application/json' },
      body: { mode: 'raw', raw: JSON.stringify({ refreshToken: pm.environment.get('refreshA') }) } }, (e, r) => {
      pm.test('replaying the old refresh token is refused', () => pm.expect([400, 401]).to.include(r.code));
    });"""),
    case("SEC-08 registration ignores a smuggled role field (mass assignment)", "POST", "/api/auth/register",
         raw='{"fullName":"Wannabe Admin","email":"sec-role-{{$timestamp}}{{$randomInt}}@foundu.test","password":"Sec!ur1ty-99Ab","studentNumber":null,"role":"Admin"}',
         tests=status(200) + "\npm.test('the new account is a Student', () => pm.expect(pm.response.json().user.role).to.equal('Student'));"),
    case("SEC-12 staff cannot open admin user management", "GET", "/api/admin/users", token="staff", tests=status(403)),
    case("SEC-27 responses carry basic security headers", "GET", "/api/lost-reports/feed",
         tests="""pm.test('X-Content-Type-Options: nosniff', () => pm.expect(pm.response.headers.get('X-Content-Type-Options')).to.equal('nosniff'));
    pm.test('no Server header advertising Kestrel', () => pm.expect(pm.response.headers.has('Server')).to.be.false);"""),
    case("SEC-28 the sixth password-reset request in a minute gets 429 Too Many Requests", "POST", "/api/auth/forgot-password",
         body={"email": "kasun@foundu.test"},
         pre="""// Requests one to five (the limit is five a minute per client); this request is the sixth.
const api = pm.environment.get('api');
const fire = n => n > 0 && pm.sendRequest({ url: api + '/api/auth/forgot-password', method: 'POST',
  header: { 'Content-Type': 'application/json' },
  body: { mode: 'raw', raw: JSON.stringify({ email: 'kasun@foundu.test' }) } }, () => fire(n - 1));
fire(5);""",
         tests=status(429) + """
pm.test('says to wait, with a Retry-After header', () => {
  pm.expect(pm.response.headers.get('Retry-After')).to.equal('60');
  pm.expect(pm.response.json().detail).to.include('wait a minute');
});"""),
    case("SEC-29 a NUL byte in a search is a 400, not a server error (ZAP finding)", "GET", "/api/lost-reports/feed?search=%00",
         tests=status(400) + "\n" + no_leak),
    case("SEC-30 a NUL byte inside a JSON body is a 400, not a server error", "POST", "/api/support/tickets", token="studentA",
         raw='{"subject":"Null \\u0000 byte","category":"Other","body":"testing a null \\u0000 character in the body"}',
         tests=status(400) + "\n" + no_leak),
    case("SETUP student A opens a support ticket", "POST", "/api/support/tickets", token="studentA",
         raw='{"subject":"Security suite ticket {{$timestamp}}","category":"Other","body":"A private question for the support team."}',
         tests="pm.test('created', () => pm.expect([200, 201]).to.include(pm.response.code)); pm.environment.set('ticketA', pm.response.json().id);"),
    case("SEC-31 IDOR: student B cannot read student A's support ticket", "GET", "/api/support/tickets/{{ticketA}}", token="studentB",
         tests=status(403, 404) + "\npm.test('no ticket text returned', () => pm.expect(pm.response.text()).to.not.include('private question'));"),
    case("SEC-32 a student cannot open the staff support queue", "GET", "/api/admin/support/tickets", token="studentA", tests=status(403)),
    case("SEC-33 staff can answer tickets but cannot delete one (admin only)", "DELETE", "/api/admin/support/tickets/{{ticketA}}", token="staff",
         tests=status(403) + """
pm.sendRequest({ url: pm.environment.get('api') + '/api/support/tickets/' + pm.environment.get('ticketA'), method: 'GET',
  header: { Authorization: 'Bearer ' + pm.environment.get('studentA') } }, (e, r) => {
  pm.test('the ticket is still there for its owner', () => pm.expect(r.code).to.equal(200));
});"""),
]

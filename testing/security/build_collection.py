"""Builds foundu-security.postman_collection.json - the API security test suite run by Newman.

Kept as a script so each case reads as one block (request + assertions) and the JSON stays
generated. Run: python3 build_collection.py && npm test
"""

import json

items: list[dict] = []


def case(name, method, path, *, token=None, body=None, raw=None, headers=None, pre="", tests="", form=None):
    hdrs = [{"key": "Content-Type", "value": "application/json"}] if (body is not None or raw is not None) else []
    if token:
        hdrs.append({"key": "Authorization", "value": "Bearer {{" + token + "}}"})
    for k, v in (headers or {}).items():
        hdrs.append({"key": k, "value": v})
    request = {"method": method, "header": hdrs, "url": path if path.startswith("{{") else "{{api}}" + path}
    if body is not None:
        request["body"] = {"mode": "raw", "raw": json.dumps(body)}
    if raw is not None:
        request["body"] = {"mode": "raw", "raw": raw}
    if form is not None:
        request["body"] = {"mode": "formdata", "formdata": form}
    event = []
    if pre:
        event.append({"listen": "prerequest", "script": {"exec": pre.strip().split("\n")}})
    if tests:
        event.append({"listen": "test", "script": {"exec": tests.strip().split("\n")}})
    items.append({"name": name, "request": request, "event": event})


status = lambda *codes: f"pm.test('status is {' or '.join(map(str, codes))}', () => pm.expect([{', '.join(map(str, codes))}]).to.include(pm.response.code));"
no_leak = """pm.test('no stack trace or server internals in the body', () => {
  const b = pm.response.text();
  pm.expect(b).to.not.match(/at FoundU\\.|Microsoft\\.|Npgsql|StackTrace|System\\.\\w+Exception/);
});"""

# ---------------------------------------------------------------- setup
case("SETUP login student A (amara)", "POST", "/api/auth/login",
     body={"email": "amara@foundu.test", "password": "{{demoPassword}}"},
     tests="pm.test('logged in', () => pm.response.to.have.status(200)); pm.environment.set('studentA', pm.response.json().accessToken); pm.environment.set('refreshA', pm.response.json().refreshToken);")
case("SETUP login staff (priya)", "POST", "/api/auth/login",
     body={"email": "priya@foundu.test", "password": "{{demoPassword}}"},
     tests="pm.test('logged in', () => pm.response.to.have.status(200)); pm.environment.set('staff', pm.response.json().accessToken);")
case("SETUP register student B", "POST", "/api/auth/register",
     raw='{"fullName":"Security B","email":"sec-b-{{$timestamp}}{{$randomInt}}@foundu.test","password":"Sec!ur1ty-{{$randomInt}}Ab","studentNumber":null}',
     tests="pm.test('registered', () => pm.response.to.have.status(200)); pm.environment.set('studentB', pm.response.json().accessToken);")
case("SETUP reference data", "GET", "/api/reference/categories", token="studentA",
     tests="""const c = pm.response.json().find(c => c.itemTypes.length);
pm.environment.set('categoryId', c.id); pm.environment.set('itemTypeId', c.itemTypes[0].id);""")
case("SETUP locations", "GET", "/api/reference/locations", token="studentA",
     tests="pm.environment.set('locationId', pm.response.json()[0].id);")
case("SETUP student A files a lost report", "POST", "/api/lost-reports", token="studentA",
     raw='{"categoryId":"{{categoryId}}","itemTypeId":"{{itemTypeId}}","primaryColor":"Black","lastSeenLocationId":"{{locationId}}","description":"Security suite report {{$timestamp}}","estimatedLostFromAt":"2026-10-01T08:00:00Z","estimatedLostToAt":"2026-10-01T09:00:00Z"}',
     tests="pm.test('created', () => pm.response.to.have.status(201)); pm.environment.set('reportA', pm.response.json().id);")

# ---------------------------------------------------------------- authentication
case("SEC-01 anonymous request to a protected endpoint is refused", "GET", "/api/lost-reports/my-reports", tests=status(401))
case("SEC-02 garbage bearer token is refused", "GET", "/api/auth/me", headers={"Authorization": "Bearer not.a.jwt"}, tests=status(401))
case("SEC-03 token with a forged role claim (signature no longer matches) is refused", "GET", "/api/admin/users",
     headers={"Authorization": "Bearer {{forged}}"},
     pre="""const [h, p, s] = pm.environment.get('studentA').split('.');
const b64 = x => Buffer.from(x, 'base64').toString();
const enc = x => Buffer.from(x).toString('base64').replace(/=+$/, '').replace(/\\+/g, '-').replace(/\\//g, '_');
const payload = JSON.parse(b64(p.replace(/-/g, '+').replace(/_/g, '/')));
for (const k of Object.keys(payload)) if (/role$/i.test(k)) payload[k] = 'Admin';
pm.environment.set('forged', [h, enc(JSON.stringify(payload)), s].join('.'));""",
     tests=status(401))
case("SEC-04 unsigned token (alg: none) is refused", "GET", "/api/auth/me",
     headers={"Authorization": "Bearer {{unsigned}}"},
     pre="""const [, p] = pm.environment.get('studentA').split('.');
const header = Buffer.from(JSON.stringify({ alg: 'none', typ: 'JWT' })).toString('base64').replace(/=+$/, '');
pm.environment.set('unsigned', `${header}.${p}.`);""",
     tests=status(401))
case("SEC-05 wrong password and unknown email get the same answer (no account enumeration)", "POST", "/api/auth/login",
     body={"email": "nobody-here@foundu.test", "password": "Wrong-pass1"},
     tests=status(401) + """
const strip = t => { const b = JSON.parse(t); delete b.traceId; return JSON.stringify(b); };
const unknown = strip(pm.response.text());
pm.sendRequest({ url: pm.environment.get('api') + '/api/auth/login', method: 'POST', header: { 'Content-Type': 'application/json' },
  body: { mode: 'raw', raw: JSON.stringify({ email: 'kasun@foundu.test', password: 'Wrong-pass1' }) } }, (e, r) => {
  pm.test('known email with wrong password: same status', () => pm.expect(r.code).to.equal(401));
  pm.test('same body apart from the trace id, so an attacker cannot tell which emails exist', () => pm.expect(strip(r.text())).to.equal(unknown));
});""")
case("SEC-06 forgot-password answers the same for unknown emails", "POST", "/api/auth/forgot-password",
     body={"email": "nobody-at-all@foundu.test"}, tests=status(202))
case("SEC-07 a refresh token cannot be used twice (rotation)", "POST", "/api/auth/refresh",
     raw='{"refreshToken":"{{refreshA}}"}',
     tests=status(200) + """
pm.environment.set('studentA', pm.response.json().accessToken);
pm.sendRequest({ url: pm.environment.get('api') + '/api/auth/refresh', method: 'POST', header: { 'Content-Type': 'application/json' },
  body: { mode: 'raw', raw: JSON.stringify({ refreshToken: pm.environment.get('refreshA') }) } }, (e, r) => {
  pm.test('replaying the old refresh token is refused', () => pm.expect([400, 401]).to.include(r.code));
});""")
case("SEC-08 registration ignores a smuggled role field (mass assignment)", "POST", "/api/auth/register",
     raw='{"fullName":"Wannabe Admin","email":"sec-role-{{$timestamp}}{{$randomInt}}@foundu.test","password":"Sec!ur1ty-99Ab","studentNumber":null,"role":"Admin"}',
     tests=status(200) + "\npm.test('the new account is a Student', () => pm.expect(pm.response.json().user.role).to.equal('Student'));")

# ---------------------------------------------------------------- authorisation (roles and ownership)
case("SEC-09 a student cannot read the staff claims queue", "GET", "/api/claims", token="studentA", tests=status(403))
case("SEC-10 a student cannot log a found item (staff only)", "POST", "/api/found-reports", token="studentA",
     body={"categoryId": "00000000-0000-0000-0000-000000000000"}, tests=status(403))
case("SEC-11 a student cannot read found items with their hidden details", "GET", "/api/found-reports", token="studentA", tests=status(403))
case("SEC-12 staff cannot open admin user management", "GET", "/api/admin/users", token="staff", tests=status(403))
case("SEC-13 staff cannot overturn a claim (admin only)", "POST", "/api/claims/00000000-0000-0000-0000-000000000001/overturn",
     token="staff", body={"reason": "x"}, tests=status(403))
case("SEC-14 IDOR: student B cannot read student A's report", "GET", "/api/lost-reports/{{reportA}}", token="studentB",
     tests=status(403, 404) + "\npm.test('no report data returned', () => pm.expect(pm.response.text()).to.not.include('Security suite report'));")
case("SEC-15 IDOR: student B cannot edit student A's report", "PUT", "/api/lost-reports/{{reportA}}", token="studentB",
     raw='{"categoryId":"{{categoryId}}","itemTypeId":"{{itemTypeId}}","primaryColor":"Red","lastSeenLocationId":"{{locationId}}","description":"hijacked by student B","estimatedLostFromAt":"2026-10-01T08:00:00Z","estimatedLostToAt":"2026-10-01T09:00:00Z"}',
     tests=status(403, 404))
case("SEC-16 IDOR: student B cannot withdraw student A's report", "POST", "/api/lost-reports/{{reportA}}/withdraw", token="studentB",
     body={"reason": "not mine"}, tests=status(403, 404))
case("SEC-17 IDOR: student B cannot read student A's report messages", "GET", "/api/lost-reports/{{reportA}}/messages", token="studentB",
     tests=status(403, 404))
case("SEC-18 owner still has their report after the attempts above", "GET", "/api/lost-reports/{{reportA}}", token="studentA",
     tests=status(200) + "\npm.test('description untouched', () => pm.expect(pm.response.json().description).to.include('Security suite report'));\npm.test('still active', () => pm.expect(pm.response.json().status).to.equal('Active'));")

# ---------------------------------------------------------------- input handling
case("SEC-19 SQL injection in the feed search is treated as text", "GET", "/api/lost-reports/feed?search=%27%20OR%201%3D1--&pageSize=50",
     tests=status(200) + "\npm.test('matches nothing, rather than every row', () => pm.expect(pm.response.json().totalCount).to.equal(0));\n" + no_leak)
case("SEC-20 malformed JSON gets a 400 problem response, no internals", "POST", "/api/lost-reports", token="studentA",
     raw='{"description": "unterminated', tests=status(400) + "\n" + no_leak)
case("SEC-21 description over the 1000-character limit is refused (boundary 1001)", "POST", "/api/lost-reports", token="studentA",
     raw='{"categoryId":"{{categoryId}}","itemTypeId":"{{itemTypeId}}","primaryColor":"Black","lastSeenLocationId":"{{locationId}}","description":"{{long}}","estimatedLostFromAt":"2026-10-01T08:00:00Z","estimatedLostToAt":"2026-10-01T09:00:00Z"}',
     pre="pm.environment.set('long', 'a'.repeat(1001));", tests=status(400))
case("SEC-22 script in a description is stored as plain text and served as JSON", "POST", "/api/lost-reports", token="studentA",
     raw='{"categoryId":"{{categoryId}}","itemTypeId":"{{itemTypeId}}","primaryColor":"Black","lastSeenLocationId":"{{locationId}}","description":"<script>alert(1)</script> black bag","estimatedLostFromAt":"2026-10-01T08:00:00Z","estimatedLostToAt":"2026-10-01T09:00:00Z"}',
     tests=status(201) + "\npm.test('served as JSON, never as HTML', () => pm.expect(pm.response.headers.get('Content-Type')).to.include('application/json'));")
case("SEC-23 a text file renamed .jpg is refused as a photo", "POST", "/api/lost-reports/{{reportA}}/photos", token="studentA",
     form=[{"key": "files", "type": "file", "src": "fixtures/not-an-image.jpg"}], tests=status(400) + "\n" + no_leak)
case("SEC-24 non-GUID ids are 404, not a server error", "GET", "/api/lost-reports/1%20OR%201=1", token="studentA", tests=status(404))

# ---------------------------------------------------------------- transport, headers, service boundary
case("SEC-25 CORS does not allow an arbitrary origin", "OPTIONS", "/api/lost-reports/feed",
     headers={"Origin": "https://evil.example", "Access-Control-Request-Method": "GET"},
     tests="pm.test('no CORS grant for evil.example', () => pm.expect(pm.response.headers.get('Access-Control-Allow-Origin')).to.not.equal('https://evil.example'));")
case("SEC-26 the AI service refuses calls without the service key", "POST", "{{ai}}/agents/run",
     body={"agent": "matching"}, tests=status(401, 403))
case("SEC-27 responses carry basic security headers", "GET", "/api/lost-reports/feed",
     tests="""pm.test('X-Content-Type-Options: nosniff', () => pm.expect(pm.response.headers.get('X-Content-Type-Options')).to.equal('nosniff'));
pm.test('no Server header advertising Kestrel', () => pm.expect(pm.response.headers.has('Server')).to.be.false);""")
case("SEC-28 forgot-password is rate limited per client (email-flooding protection)", "POST", "/api/auth/forgot-password",
     body={"email": "kasun@foundu.test"},
     tests="""const send = (n, codes) => n === 0
  ? pm.test('a burst of 15 reset requests is throttled with 429', () => pm.expect(codes).to.include(429))
  : pm.sendRequest({ url: pm.environment.get('api') + '/api/auth/forgot-password', method: 'POST', header: { 'Content-Type': 'application/json' },
      body: { mode: 'raw', raw: JSON.stringify({ email: 'kasun@foundu.test' }) } }, (e, r) => send(n - 1, codes.concat(r.code)));
send(14, [pm.response.code]);""")

case("SEC-29 a NUL byte in a search is a 400, not a server error (ZAP finding)", "GET", "/api/lost-reports/feed?search=%00",
     tests=status(400) + "\n" + no_leak)
case("SEC-30 a NUL byte inside a JSON body is a 400, not a server error", "POST", "/api/support/tickets", token="studentA",
     raw='{"subject":"Null \\u0000 byte","category":"Other","body":"testing a null \\u0000 character in the body"}',
     tests=status(400) + "\n" + no_leak)

collection = {
    "info": {
        "name": "FoundU API security tests",
        "description": "Authentication, authorisation (roles and ownership), input handling and transport checks against the running API.",
        "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json",
    },
    "item": items,
}
with open("foundu-security.postman_collection.json", "w") as f:
    json.dump(collection, f, indent=2)
with open("local.postman_environment.json", "w") as f:
    json.dump({"name": "local", "values": [
        {"key": "api", "value": "http://localhost:5292", "enabled": True},
        {"key": "ai", "value": "http://localhost:8000", "enabled": True},
        {"key": "demoPassword", "value": "Demo!Pass2026", "enabled": True},
    ]}, f, indent=2)
print(f"{len(items)} requests written")

"""Member 2 - Ranasinghe R.G.P.D (IT24100910)

Lost reports: ownership (IDOR), validation limits, injection and photo uploads.
"""

from .common import case, no_leak, status

CASES = [
    case("SEC-14 IDOR: student B cannot read student A's report", "GET", "/api/lost-reports/{{reportA}}", token="studentB",
         tests=status(403, 404) + "\npm.test('no report data returned', () => pm.expect(pm.response.text()).to.not.include('Security suite report'));"),
    case("SEC-15 IDOR: student B cannot edit student A's report", "PUT", "/api/lost-reports/{{reportA}}", token="studentB",
         raw='{"categoryId":"{{categoryId}}","itemTypeId":"{{itemTypeId}}","primaryColor":"Red","lastSeenLocationId":"{{locationId}}","description":"hijacked by student B","estimatedLostFromAt":"2026-10-01T08:00:00Z","estimatedLostToAt":"2026-10-01T09:00:00Z"}',
         tests=status(403, 404)),
    case("SEC-16 IDOR: student B cannot withdraw student A's report", "POST", "/api/lost-reports/{{reportA}}/withdraw", token="studentB",
         body={"reason": "not mine"}, tests=status(403, 404)),
    case("SEC-17 IDOR: student B cannot read student A's report messages", "GET", "/api/lost-reports/{{reportA}}/messages", token="studentB",
         tests=status(403, 404)),
    case("SEC-18 owner still has their report after the attempts above", "GET", "/api/lost-reports/{{reportA}}", token="studentA",
         tests=status(200) + "\npm.test('description untouched', () => pm.expect(pm.response.json().description).to.include('Security suite report'));\npm.test('still active', () => pm.expect(pm.response.json().status).to.equal('Active'));"),
    case("SEC-19 SQL injection in the feed search is treated as text", "GET", "/api/lost-reports/feed?search=%27%20OR%201%3D1--&pageSize=50",
         tests=status(200) + "\npm.test('matches nothing, rather than every row', () => pm.expect(pm.response.json().totalCount).to.equal(0));\n" + no_leak),
    case("SEC-20 malformed JSON gets a 400 problem response, no internals", "POST", "/api/lost-reports", token="studentA",
         raw='{"description": "unterminated', tests=status(400) + "\n" + no_leak),
    case("SEC-21 description over the 1000-character limit is refused (boundary 1001)", "POST", "/api/lost-reports", token="studentA",
         raw='{"categoryId":"{{categoryId}}","itemTypeId":"{{itemTypeId}}","primaryColor":"Black","lastSeenLocationId":"{{locationId}}","description":"{{long}}","estimatedLostFromAt":"2026-10-01T08:00:00Z","estimatedLostToAt":"2026-10-01T09:00:00Z"}',
         pre="pm.environment.set('long', 'a'.repeat(1001));", tests=status(400)),
    case("SEC-22 script in a description is stored as plain text and served as JSON", "POST", "/api/lost-reports", token="studentA",
         raw='{"categoryId":"{{categoryId}}","itemTypeId":"{{itemTypeId}}","primaryColor":"Black","lastSeenLocationId":"{{locationId}}","description":"<script>alert(1)</script> black bag","estimatedLostFromAt":"2026-10-01T08:00:00Z","estimatedLostToAt":"2026-10-01T09:00:00Z"}',
         tests=status(201) + "\npm.test('served as JSON, never as HTML', () => pm.expect(pm.response.headers.get('Content-Type')).to.include('application/json'));"),
    case("SEC-23 a text file renamed .jpg is refused as a photo", "POST", "/api/lost-reports/{{reportA}}/photos", token="studentA",
         form=[{"key": "files", "type": "file", "src": "fixtures/not-an-image.jpg"}], tests=status(400) + "\n" + no_leak),
    case("SEC-24 non-GUID ids are 404, not a server error", "GET", "/api/lost-reports/1%20OR%201=1", token="studentA", tests=status(404)),
]

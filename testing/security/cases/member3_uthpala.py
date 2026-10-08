"""Member 3 - Uthpala W.A.S (IT24101028)

Found items and the desk: staff-only endpoints and the hidden verification detail.
"""

from .common import case, no_leak, status

CASES = [
    case("SEC-10 a student cannot log a found item (staff only)", "POST", "/api/found-reports", token="studentA",
         body={"categoryId": "00000000-0000-0000-0000-000000000000"}, tests=status(403)),
    case("SEC-11 a student cannot read found items with their hidden details", "GET", "/api/found-reports", token="studentA", tests=status(403)),
    case("SETUP storage locations", "GET", "/api/reference/storage-locations", token="staff",
         tests="pm.environment.set('storageId', pm.response.json()[0].id);"),
    case("SETUP staff log a found item with a hidden detail", "POST", "/api/found-reports", token="staff",
         raw='{"categoryId":"{{categoryId}}","itemTypeId":"{{itemTypeId}}","primaryColor":"Teal","foundLocationId":"{{locationId}}","storageLocationId":"{{storageId}}","generalDescription":"Security suite item {{$timestamp}}","privateVerificationDetails":"Hidden mark SECRET-7731 inside","foundAt":"2026-10-01T10:00:00Z"}',
         tests="pm.test('logged', () => pm.response.to.have.status(201)); pm.environment.set('foundItem', pm.response.json().id); pm.environment.set('foundText', pm.response.json().generalDescription);"),
    case("SEC-37 staff see the hidden detail on the item they logged", "GET", "/api/found-reports/{{foundItem}}", token="staff",
         tests=status(200) + "\npm.test('hidden detail is there for staff', () => pm.expect(pm.response.json().privateVerificationDetails).to.include('SECRET-7731'));"),
    case("SEC-38 a found item with an empty description is refused", "POST", "/api/found-reports", token="staff",
         raw='{"categoryId":"{{categoryId}}","itemTypeId":"{{itemTypeId}}","foundLocationId":"{{locationId}}","storageLocationId":"{{storageId}}","generalDescription":"","foundAt":"2026-10-01T10:00:00Z"}',
         tests=status(400) + "\n" + no_leak),
    case("SEC-39 the Found board never shows a student the hidden detail", "GET", "/api/found-posts/feed?pageSize=100&search=Security%20suite%20item", token="studentA",
         tests=status(200) + """
pm.test('the item is on the board', () => pm.expect(pm.response.json().items.map(i => i.id)).to.include(pm.environment.get('foundItem')));
pm.test('its hidden detail is not', () => pm.expect(pm.response.text()).to.not.include('SECRET-7731'));"""),
    case("SEC-40 a student cannot look up a desk hand-in code", "GET", "/api/desk/codes/123456", token="studentA", tests=status(403)),
    case("SEC-41 a student cannot run the matching agent (staff only)", "POST", "/api/match-suggestions/generate-ai", token="studentA",
         raw='{"lostReportId":"{{reportA}}","foundReportId":"{{foundItem}}"}', tests=status(403)),
    case("SEC-42 a student cannot read the match review for an item", "GET", "/api/match-suggestions/review-for-item/{{foundItem}}", token="studentA",
         tests=status(403)),
    case("SEC-43 a desk code that is not six digits is refused cleanly", "GET", "/api/desk/codes/12ab%27--", token="staff",
         tests=status(400, 404) + "\n" + no_leak),
]

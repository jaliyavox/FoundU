"""Member 4 - Braveena S (IT24100354)

Claims: the staff queue and admin-only decisions.
"""

from .common import case, no_leak, status

CASES = [
    case("SEC-09 a student cannot read the staff claims queue", "GET", "/api/claims", token="studentA", tests=status(403)),
    case("SEC-13 staff cannot overturn a claim (admin only)", "POST", "/api/claims/00000000-0000-0000-0000-000000000001/overturn",
         token="staff", body={"reason": "x"}, tests=status(403)),
    case("SETUP storage locations", "GET", "/api/reference/storage-locations", token="staff",
         tests="pm.environment.set('storageId', pm.response.json()[0].id);"),
    case("SETUP staff log a found item with a hidden detail", "POST", "/api/found-reports", token="staff",
         raw='{"categoryId":"{{categoryId}}","itemTypeId":"{{itemTypeId}}","primaryColor":"Teal","foundLocationId":"{{locationId}}","storageLocationId":"{{storageId}}","generalDescription":"Security suite item {{$timestamp}}","privateVerificationDetails":"Hidden mark SECRET-7731 inside","foundAt":"2026-10-01T10:00:00Z"}',
         tests="pm.test('logged', () => pm.response.to.have.status(201)); pm.environment.set('foundItem', pm.response.json().id); pm.environment.set('foundText', pm.response.json().generalDescription);"),
    case("SEC-44 student A claims the item in their own words", "POST", "/api/claims/without-report", token="studentA",
         raw='{"foundReportId":"{{foundItem}}","description":"Teal bag, my notes and a pencil case are inside it"}',
         tests=status(201) + "\npm.environment.set('claimA', pm.response.json().id);"),
    case("SEC-45 the claimant never sees the hidden detail or a collection code", "GET", "/api/claims/{{claimA}}", token="studentA",
         tests=status(200) + """
pm.test('no hidden detail', () => pm.expect(pm.response.text()).to.not.include('SECRET-7731'));
pm.test('no collection code before approval', () => pm.expect(pm.response.json().collectionCode == null).to.be.true);"""),
    case("SEC-46 IDOR: student B cannot read student A's claim", "GET", "/api/claims/{{claimA}}", token="studentB",
         tests=status(403, 404) + "\npm.test('no claim data', () => pm.expect(pm.response.text()).to.not.include('pencil case'));"),
    case("SEC-47 student B cannot answer the questions on student A's claim", "POST", "/api/claims/{{claimA}}/answers", token="studentB",
         body={"answers": [{"questionId": "00000000-0000-0000-0000-000000000001", "answerText": "guessing"}]},
         tests=status(403, 404)),
    case("SEC-48 a student cannot approve a claim (staff only)", "POST", "/api/claims/{{claimA}}/decision", token="studentA",
         body={"decision": "Approved", "reason": None}, tests=status(403)),
    case("SEC-49 a student cannot collect an item by code (staff only)", "POST", "/api/claims/collect", token="studentA",
         body={"code": "123456", "ownerIdChecked": True}, tests=status(403)),
    case("SEC-50 staff cannot hand over without checking the owner's ID", "POST", "/api/claims/collect", token="staff",
         body={"code": "123456", "ownerIdChecked": False}, tests=status(400) + "\n" + no_leak),
    case("SEC-51 an unknown collection code finds nothing", "POST", "/api/claims/collect", token="staff",
         body={"code": "000000", "ownerIdChecked": True}, tests=status(404) + "\n" + no_leak),
]

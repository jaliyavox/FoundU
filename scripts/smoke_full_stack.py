"""Opt-in local integration smoke test; creates a labelled student and resolved test item.

Run with ai/.venv/bin/python scripts/smoke_full_stack.py. Supply FOUNDU_SMOKE_ADMIN_PASSWORD
and optionally FOUNDU_SMOKE_ADMIN_EMAIL / FOUNDU_SMOKE_API_URL in the environment.
Requires the API, PostgreSQL and authenticated FastAPI service to be running.
"""

import datetime
import json
import os
import secrets

import httpx

base = os.environ.get("FOUNDU_SMOKE_API_URL", "http://localhost:5292/api").rstrip("/")
password = os.environ.get("FOUNDU_SMOKE_ADMIN_PASSWORD")
if not password:
    raise SystemExit("Set FOUNDU_SMOKE_ADMIN_PASSWORD to a local test admin password.")
client = httpx.Client(timeout=45)


def req(method, path, token=None, data=None, status=200):
    r = client.request(
        method,
        base + path,
        headers={"Authorization": "Bearer " + token} if token else {},
        json=data,
    )
    if r.status_code != status:
        raise RuntimeError(
            f"{method} {path}: expected {status}, got {r.status_code}: {r.text[:300]}"
        )
    return r.json() if r.content else None


admin = req(
    "POST",
    "/auth/login",
    data={
        "email": os.environ.get("FOUNDU_SMOKE_ADMIN_EMAIL", "admin@foundu.com"),
        "password": password,
    },
)["accessToken"]
suffix = secrets.token_hex(5)
student = req(
    "POST",
    "/auth/register",
    data={
        "fullName": "Completion Smoke " + suffix,
        "email": f"completion-{suffix}@foundu.test",
        "password": secrets.token_urlsafe(24) + "aA9",
        "studentNumber": None,
    },
    status=200,
)["accessToken"]
categories = req("GET", "/reference/categories", student)
category = next(c for c in categories if c["itemTypes"])
location = req("GET", "/reference/locations", student)[0]["id"]
storage = req("GET", "/reference/storage-locations", admin)[0]["id"]
now = datetime.datetime.now(datetime.timezone.utc)
start = (now - datetime.timedelta(hours=2)).isoformat().replace("+00:00", "Z")
end = (now - datetime.timedelta(hours=1)).isoformat().replace("+00:00", "Z")
common = {
    "categoryId": category["id"],
    "itemTypeId": category["itemTypes"][0]["id"],
    "primaryColor": "Black",
}
lost = req(
    "POST",
    "/lost-reports",
    student,
    {
        **common,
        "lastSeenLocationId": location,
        "description": "Completion smoke test black item " + suffix,
        "estimatedLostFromAt": start,
        "estimatedLostToAt": end,
    },
    201,
)
found = req(
    "POST",
    "/found-reports",
    admin,
    {
        **common,
        "foundLocationId": location,
        "storageLocationId": storage,
        "generalDescription": "Completion smoke test black item " + suffix,
        "privateVerificationDetails": "blue keychain",
        "foundAt": end,
    },
    201,
)
match = req(
    "POST",
    "/match-suggestions/generate-ai",
    admin,
    {"lostReportId": lost["id"], "foundReportId": found["id"]},
)
assert match["recommendation"] == "match_candidate", match
claim = req(
    "POST",
    "/claims",
    student,
    {"lostReportId": lost["id"], "foundReportId": found["id"]},
    201,
)
claim = req("POST", f'/claims/{claim["id"]}/questions/generate', admin)
assert claim["status"] == "WaitingForAnswer", claim["status"]
student_claim = req("GET", f'/claims/{claim["id"]}', student)
assert "blue keychain" not in json.dumps(student_claim)
req("GET", f'/claims/{claim["id"]}/agent-runs', student, status=403)
claim = req(
    "POST",
    f'/claims/{claim["id"]}/answers',
    student,
    {
        "answers": [
            {"questionId": q["id"], "answerText": "blue keychain"}
            for q in claim["questions"]
        ]
    },
)
assert claim["status"] == "UnderReview", claim["status"]
claim = req(
    "POST",
    f'/claims/{claim["id"]}/decision',
    admin,
    {"decision": "Approved", "reason": "Completion smoke test: verified at desk."},
)
assert claim["collectionCode"] is None
owner = req("GET", f'/claims/{claim["id"]}', student)
code = owner["collectionCode"]
assert code
collected = req("POST", "/claims/collect", admin, {"code": code})
assert collected["collectedAt"] and collected["collectionCode"] is None
req("POST", "/claims/collect", admin, {"code": code}, 404)
assert req("GET", f'/lost-reports/{lost["id"]}', student)["status"] == "Resolved"
assert req("GET", f'/found-reports/{found["id"]}', admin)["status"] == "Returned"
notes = req("GET", "/notifications", student)["items"]
assert notes
req("POST", f'/notifications/{notes[0]["id"]}/read', student)
all_read = req("POST", "/notifications/read-all", student)
count = req("GET", "/notifications/unread-count", student)["unread"]
assert count == 0
assert all_read["unread"] == count, all_read
print(
    json.dumps(
        {
            "workflow": "passed",
            "claimId": claim["id"],
            "notificationCount": len(notes),
            "remainingUnread": count,
            "markAllResponse": all_read,
            "agentRuns": len(req("GET", f'/claims/{claim["id"]}/agent-runs', admin)),
        }
    )
)
client.close()

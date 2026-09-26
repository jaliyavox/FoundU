"""Wipe the local development data and rebuild a demo that exercises every flow.

    python3 scripts/demo_seed.py

Destructive: it deletes every report, item, claim, ticket, handover, message, notification
and non-admin account in the local database. Reference data (categories, item types, campus
and storage locations) and the seeded admin are kept. Never point it at anything but a local
development database.

Everything is created through the real API rather than written into tables, so the seed goes
through the same validation, notifications and state machine as a person would.
"""

import json
import os
import subprocess
import sys
import urllib.error
import urllib.request
from datetime import datetime, timedelta, timezone

API = os.environ.get("FOUNDU_API", "http://localhost:5292")
CONTAINER = os.environ.get("FOUNDU_PG_CONTAINER", "foundu-postgres")
DB_USER = os.environ.get("FOUNDU_PG_USER", "foundu")
DB_NAME = os.environ.get("FOUNDU_PG_DB", "foundu")
ADMIN_EMAIL = os.environ.get("FOUNDU_ADMIN_EMAIL", "admin@foundu.com")

# Tables holding activity, in dependency order. Reference data and the admin stay.
WIPE = [
    "AgentSteps", "AgentRuns", "ApprovalDecisions", "ClaimAnswers", "VerificationQuestions",
    "ClaimStatusHistories", "Claims", "MatchStatusHistories", "MatchSuggestions",
    "LostReportMessages", "LostReportFoundClaims", "LostReportStatusHistories", "LostItemPhotos",
    "FoundReportMessages", "FoundReportStatusHistories", "FoundItemPhotos", "StorageTransfers",
    "SupportTicketMessages", "SupportTickets", "HonorAwards", "Notifications",
    "DeviceRegistrations", "RefreshTokens", "AuditLogs", "FoundReports", "LostReports",
    "ai_workflow_states",
]


def psql(sql: str) -> str:
    result = subprocess.run(
        ["docker", "exec", CONTAINER, "psql", "-U", DB_USER, "-d", DB_NAME, "-Atc", sql],
        capture_output=True, text=True,
    )
    if result.returncode != 0:
        raise SystemExit(f"psql failed: {result.stderr.strip()}")
    return result.stdout.strip()


def call(path, body=None, token=None, method=None, expect=(200, 201)):
    request = urllib.request.Request(
        API + path,
        data=json.dumps(body).encode() if body is not None else None,
        headers={
            "Content-Type": "application/json",
            **({"Authorization": "Bearer " + token} if token else {}),
        },
        method=method or ("POST" if body is not None else "GET"),
    )
    try:
        with urllib.request.urlopen(request) as response:
            payload = json.loads(response.read() or b"null")
            status = response.status
    except urllib.error.HTTPError as error:
        payload = json.loads(error.read() or b"null")
        status = error.code

    if status not in expect:
        raise SystemExit(f"{method or 'GET'} {path} -> {status}: {json.dumps(payload)[:400]}")
    return payload


def login(email, password):
    return call("/api/auth/login", {"email": email, "password": password})["accessToken"]


def register(name, email, password, student_number=None):
    call("/api/auth/register", {
        "fullName": name, "email": email, "password": password, "studentNumber": student_number,
    }, expect=(200, 201, 409))
    return login(email, password)


def utc(hours_ago=0):
    return (datetime.now(timezone.utc) - timedelta(hours=hours_ago)).isoformat().replace("+00:00", "Z")


def main():
    admin_password = os.environ.get("FOUNDU_ADMIN_PASSWORD")
    if not admin_password:
        settings = "api/src/FoundU.Api/appsettings.Development.json"
        try:
            admin_password = json.load(open(settings))["Seed"]["DevAdminPassword"]
        except Exception:
            raise SystemExit("Set FOUNDU_ADMIN_PASSWORD, or run from the repository root.")

    if "localhost" not in API and "127.0.0.1" not in API:
        raise SystemExit(f"Refusing to seed {API} - this script is for local development only.")

    print(f"Wiping activity in {DB_NAME}…")
    psql("TRUNCATE " + ", ".join(f'"{table}"' for table in WIPE) + " RESTART IDENTITY CASCADE;")
    psql(f"DELETE FROM \"AppUsers\" WHERE \"Email\" <> '{ADMIN_EMAIL}';")

    admin = login(ADMIN_EMAIL, admin_password)
    categories = call("/api/reference/categories", token=admin)
    locations = call("/api/reference/locations", token=admin)
    storage = call("/api/reference/storage-locations", token=admin)

    types = {t["name"]: {**t, "categoryId": c["id"]} for c in categories for t in c["itemTypes"]}
    places = {location["name"]: location["id"] for location in locations}
    shelf = storage[0]["id"]

    def item(name):
        if name not in types:
            raise SystemExit(f"Item type '{name}' is not in the reference data.")
        return types[name]

    def place(name):
        return places.get(name) or next(iter(places.values()))

    print("Creating people…")
    people = {
        "amara": register("Amara Perera", "amara@foundu.test", "Demo!Pass2026", "IT26001001"),
        "dev": register("Dev Fernando", "dev@foundu.test", "Demo!Pass2026", "IT26001002"),
        "nadia": register("Nadia Silva", "nadia@foundu.test", "Demo!Pass2026", "IT26001003"),
        "kasun": register("Kasun Jay", "kasun@foundu.test", "Demo!Pass2026", "IT26001004"),
        "desk": register("Priya Desk", "priya@foundu.test", "Demo!Pass2026", None),
    }
    # One of them works the desk. Registration only makes Students, so the role moves here.
    psql("UPDATE \"AppUsers\" SET \"Role\" = 'Staff' WHERE \"Email\" = 'priya@foundu.test';")
    people["desk"] = login("priya@foundu.test", "Demo!Pass2026")

    def report(token, type_name, location_name, description, colour, hours_ago=4):
        kind = item(type_name)
        return call("/api/lost-reports", {
            "categoryId": kind["categoryId"], "itemTypeId": kind["id"],
            "lastSeenLocationId": place(location_name), "description": description,
            "primaryColor": colour, "secondaryColor": None,
            "estimatedLostFromAt": utc(hours_ago + 2), "estimatedLostToAt": utc(hours_ago),
        }, token)

    print("Lost reports…")
    # 1. Plain and open - what the feed is mostly made of.
    open_report = report(people["amara"], "Water Bottle", "Library",
                         "Blue metal bottle with a dented lid and a university sticker.", "Blue")
    # 2. Somebody said they found it and is messaging about it.
    talking = report(people["amara"], "Headphones", "Main Auditorium",
                     "Black over-ear headphones in a hard case.", "Black", 8)
    # 3. A handover on its way to a desk.
    walking = report(people["dev"], "Wallet", "Cafeteria",
                     "Brown leather wallet, bus pass and library card inside.", "Brown", 12)
    # 4. A handover already at a desk, waiting to be collected.
    at_desk = report(people["dev"], "Laptop", "Lecture Hall B12",
                     "Grey laptop in a padded sleeve covered in stickers.", "Grey", 20)
    # 5. One that went all the way home.
    finished = report(people["nadia"], "Backpack", "Cafeteria",
                      "Black backpack, two zips, maths notes inside.", "Black", 30)
    # 6. Given up on.
    abandoned = report(people["nadia"], "Umbrella", "Parking Lot",
                       "Red folding umbrella with a wooden handle.", "Red", 40)

    print("Conversations…")
    call(f"/api/lost-reports/{talking['id']}/found-claims", {}, people["kasun"])
    call(f"/api/lost-reports/{talking['id']}/messages",
         {"body": "I picked these up after the lecture - is there a name on the case?"}, people["kasun"])
    thread = call(f"/api/lost-reports/{talking['id']}/messages", token=people["amara"])
    call(f"/api/lost-reports/{talking['id']}/messages",
         {"body": "Yes - A. Perera on a strip of tape inside. Thank you!",
          "recipientId": thread[0]["counterpartId"]}, people["amara"])

    print("Handovers…")
    walking_code = call(f"/api/lost-reports/{walking['id']}/handover", {}, people["kasun"])["code"]

    desk_code = call(f"/api/lost-reports/{at_desk['id']}/handover", {}, people["nadia"])["code"]
    call(f"/api/handovers/by-code/{desk_code}/receive",
         {"storageLocationId": shelf, "note": "Green crate behind the counter."}, people["desk"])

    done_code = call(f"/api/lost-reports/{finished['id']}/handover", {}, people["kasun"])["code"]
    call(f"/api/handovers/by-code/{done_code}/receive", {"storageLocationId": shelf}, people["desk"])
    call(f"/api/handovers/by-code/{done_code}/release",
         {"ownerIdChecked": True, "note": "Student ID IT26001003, name matched."}, people["desk"])

    call(f"/api/lost-reports/{abandoned['id']}/withdraw", {"reason": "Found it at home."}, people["nadia"])

    print("Found posts…")
    def found_post(token, type_name, location_name, description, colour, hours_ago=3):
        kind = item(type_name)
        return call("/api/found-posts", {
            "categoryId": kind["categoryId"], "itemTypeId": kind["id"],
            "foundLocationId": place(location_name), "description": description,
            "primaryColor": colour, "foundAt": utc(hours_ago),
        }, token)

    # Still with the finder, and somebody is asking about it.
    keys = found_post(people["dev"], "House Keys", "Cafeteria",
                      "Bunch of keys on a blue lanyard, three keys and a locker tag.", "Blue")
    call(f"/api/found-posts/{keys['id']}/messages",
         {"body": "I think those are mine - is there a small brass key on the ring?"}, people["amara"])

    # Handed in and confirmed by the desk, so it is claimable.
    card = found_post(people["kasun"], "Student Card", "Library", "Student ID card found on a study desk.", None, 26)
    call(f"/api/found-posts/{card['id']}/confirm", {
        "storageLocationId": shelf,
        "privateVerificationDetails": "Card number ends 4417.",
        "generalDescription": "Student ID card handed in at the library desk.",
    }, people["desk"])

    print("Desk-logged items and a claim…")
    phone_type = item("Phone")
    call("/api/found-reports", {
        "categoryId": phone_type["categoryId"], "itemTypeId": phone_type["id"],
        "foundLocationId": place("Cafeteria"), "storageLocationId": shelf,
        "generalDescription": "White phone with a cracked corner, clear case.",
        "privateVerificationDetails": "Lock screen shows a photo of a grey cat.",
        "primaryColor": "White", "secondaryColor": None, "foundAt": utc(6),
    }, people["desk"], expect=(200, 201))

    print("Support tickets…")
    ticket = call("/api/support/tickets", {
        "subject": "My collection code will not scan",
        "category": "Collection",
        "body": "I was told to collect my laptop but the desk says the code is not recognised.",
    }, people["dev"])
    call(f"/api/support/tickets/{ticket['id']}/messages",
         {"body": "Sorry about that - come to the library desk and ask for Priya, I will check it by hand."},
         people["desk"])

    resolved_ticket = call("/api/support/tickets", {
        "subject": "Can I change the email on my account?",
        "category": "Account",
        "body": "I am switching to my university address and want my reports to follow me.",
    }, people["nadia"])
    call(f"/api/support/tickets/{resolved_ticket['id']}/messages",
         {"body": "Yes - Account settings, then Save changes. It asks for your password."}, people["desk"])
    call(f"/api/admin/support/tickets/{resolved_ticket['id']}",
         {"status": "Resolved", "assignedToUserId": None}, people["desk"], method="PUT")

    print()
    print("Done. Everyone's password is Demo!Pass2026")
    print(f"  amara@foundu.test   owner: open report, a conversation, asking about found keys")
    print(f"  dev@foundu.test     owner: a wallet on its way in, a laptop waiting at the desk, an open ticket")
    print(f"  nadia@foundu.test   owner: one item collected and home, one withdrawn, one resolved ticket")
    print(f"  kasun@foundu.test   finder: carrying a wallet, handed two in, honor points earned")
    print(f"  priya@foundu.test   staff: the desk, the support queue, the handover codes")
    print(f"  {ADMIN_EMAIL}   admin")
    print()
    print(f"  Handover in flight (Kasun carrying Dev's wallet):  {walking_code}")
    print(f"  Waiting at the desk (Dev's laptop):                {desk_code}")


if __name__ == "__main__":
    sys.exit(main())

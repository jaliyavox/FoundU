"""Builds foundu-security.postman_collection.json - the API security test suite run by Newman.

One folder per member (the endpoints of their business component) and one for the group,
each starting with the same SETUP sign-ins so it runs on its own. The cases live in
cases/<member>.py, so each reads as one block (request + assertions) and the JSON stays
generated. Run: python3 build_collection.py, then for one folder:
  npx newman run foundu-security.postman_collection.json -e local.postman_environment.json --folder "Member 1"
"""

import json
import os

from cases import group, member1_jaliya, member2_ranasinghe, member3_uthpala, member4_braveena
from cases.common import setup

FOLDERS = [
    ("Member 1", member1_jaliya),
    ("Member 2", member2_ranasinghe),
    ("Member 3", member3_uthpala),
    ("Member 4", member4_braveena),
    ("Group", group),
]

here = os.path.dirname(os.path.abspath(__file__))
folders = [
    {"name": name, "description": module.__doc__.strip(), "item": setup() + module.CASES}
    for name, module in FOLDERS
]

collection = {
    "info": {
        "name": "FoundU API security tests",
        "description": "Authentication, authorisation (roles and ownership), input handling and transport checks against the running API, one folder per member.",
        "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json",
    },
    "item": folders,
}
with open(os.path.join(here, "foundu-security.postman_collection.json"), "w") as f:
    json.dump(collection, f, indent=2)
with open(os.path.join(here, "local.postman_environment.json"), "w") as f:
    json.dump({"name": "local", "values": [
        {"key": "api", "value": "http://localhost:5292", "enabled": True},
        {"key": "ai", "value": "http://localhost:8000", "enabled": True},
        {"key": "demoPassword", "value": "Demo!Pass2026", "enabled": True},
    ]}, f, indent=2)
for name, module in FOLDERS:
    print(f"{name}: {len(module.CASES)} cases")

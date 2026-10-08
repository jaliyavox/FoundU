"""Member 3 - Uthpala W.A.S (IT24101028)

Found items and the desk: staff-only endpoints and the hidden verification detail.
"""

from .common import case, no_leak, status

CASES = [
    case("SEC-10 a student cannot log a found item (staff only)", "POST", "/api/found-reports", token="studentA",
         body={"categoryId": "00000000-0000-0000-0000-000000000000"}, tests=status(403)),
    case("SEC-11 a student cannot read found items with their hidden details", "GET", "/api/found-reports", token="studentA", tests=status(403)),
]

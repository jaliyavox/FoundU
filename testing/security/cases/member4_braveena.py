"""Member 4 - Braveena S (IT24100354)

Claims: the staff queue and admin-only decisions.
"""

from .common import case, no_leak, status

CASES = [
    case("SEC-09 a student cannot read the staff claims queue", "GET", "/api/claims", token="studentA", tests=status(403)),
    case("SEC-13 staff cannot overturn a claim (admin only)", "POST", "/api/claims/00000000-0000-0000-0000-000000000001/overturn",
         token="staff", body={"reason": "x"}, tests=status(403)),
]

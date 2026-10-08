"""The request builder, shared assertions and the sign-in steps every folder starts with.

Each folder runs on its own (newman --folder "Member 2", or one folder in the Postman
Collection Runner), so each one begins with the same SETUP requests.
"""

import json


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
    return {"name": name, "request": request, "event": event}


status = lambda *codes: f"pm.test('status is {' or '.join(map(str, codes))}', () => pm.expect([{', '.join(map(str, codes))}]).to.include(pm.response.code));"
no_leak = """pm.test('no stack trace or server internals in the body', () => {
  const b = pm.response.text();
  pm.expect(b).to.not.match(/at FoundU\\.|Microsoft\\.|Npgsql|StackTrace|System\\.\\w+Exception/);
});"""


def setup():
    return [
        case("SETUP login student A (amara)", "POST", "/api/auth/login",
             body={"email": "amara@foundu.test", "password": "{{demoPassword}}"},
             tests="pm.test('logged in', () => pm.response.to.have.status(200)); pm.environment.set('studentA', pm.response.json().accessToken); pm.environment.set('refreshA', pm.response.json().refreshToken);"),
        case("SETUP login staff (priya)", "POST", "/api/auth/login",
             body={"email": "priya@foundu.test", "password": "{{demoPassword}}"},
             tests="pm.test('logged in', () => pm.response.to.have.status(200)); pm.environment.set('staff', pm.response.json().accessToken);"),
        case("SETUP register student B", "POST", "/api/auth/register",
             raw='{"fullName":"Security B","email":"sec-b-{{$timestamp}}{{$randomInt}}@foundu.test","password":"Sec!ur1ty-{{$randomInt}}Ab","studentNumber":null}',
             tests="pm.test('registered', () => pm.response.to.have.status(200)); pm.environment.set('studentB', pm.response.json().accessToken);"),
        case("SETUP reference data", "GET", "/api/reference/categories", token="studentA",
             tests="""const c = pm.response.json().find(c => c.itemTypes.length);
pm.environment.set('categoryId', c.id); pm.environment.set('itemTypeId', c.itemTypes[0].id);"""),
        case("SETUP locations", "GET", "/api/reference/locations", token="studentA",
             tests="pm.environment.set('locationId', pm.response.json()[0].id);"),
        case("SETUP student A files a lost report", "POST", "/api/lost-reports", token="studentA",
             raw='{"categoryId":"{{categoryId}}","itemTypeId":"{{itemTypeId}}","primaryColor":"Black","lastSeenLocationId":"{{locationId}}","description":"Security suite report {{$timestamp}}","estimatedLostFromAt":"2026-10-01T08:00:00Z","estimatedLostToAt":"2026-10-01T09:00:00Z"}',
             tests="pm.test('created', () => pm.response.to.have.status(201)); pm.environment.set('reportA', pm.response.json().id);"),
    ]

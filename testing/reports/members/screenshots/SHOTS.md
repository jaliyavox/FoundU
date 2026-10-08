# Screenshots for Section 10

Save each image in this folder under the file name given. The **Section** column is where it goes
in the Software Testing Report (10.x is the member: 10.1 Jaliya, 10.2 Ranasinghe, 10.3 Uthpala,
10.4 Braveena). `section10.md` has a `[ SCREENSHOT: <file name> ]` placeholder at each spot.

Before you start: the stack is running (`testing/start-stack.sh`), and in each new terminal:

```bash
cd ~/UNI/my/Github/FoundU
export TEST_DATABASE_URL="Host=localhost;Port=5434;Database=foundu_test;Username=foundu;Password=foundu"
clear    # so the connection string is not on screen
```

Never show a password or token in a screenshot: crop or blur it.

`{n}` is the member number, `{name}` the folder name: 1 `member1_jaliya`, 2 `member2_ranasinghe`,
3 `member3_uthpala`, 4 `member4_braveena`. Member tags: `Member1-Jaliya`, `Member2-Ranasinghe`,
`Member3-Uthpala`, `Member4-Braveena`.

| # | File name | Section | Member | What must be on screen | How to get it |
|---:|---|---|---|---|---|
| 1 | `m1-db-run.png` | 10.1.2 | Jaliya | each test Passed, total `Passed: 2` | `dotnet test api/tests/FoundU.Tests --filter "Category=PostgreSql&Member=Member1-Jaliya" --logger "console;verbosity=normal"` |
| 2 | `m2-db-run.png` | 10.2.2 | Ranasinghe | each test Passed, total `Passed: 1` | same, `Member=Member2-Ranasinghe` |
| 3 | `m3-db-run.png` | 10.3.2 | Uthpala | each test Passed, total `Passed: 3` | same, `Member=Member3-Uthpala` |
| 4 | `m4-db-run.png` | 10.4.2 | Braveena | each test Passed, total `Passed: 6` | same, `Member=Member4-Braveena` |
| 5 | `m3-db-editor.png` | 10.3.2 | Uthpala | `Member3DatabaseTests.cs` open in the editor beside its passing run | open `api/tests/FoundU.Tests/Member3_Uthpala/Member3DatabaseTests.cs`, run #3 in the VS Code terminal below it |
| 6 | `m1-e2e-report.png` | 10.1.5 | Jaliya | Playwright HTML report, their specs, all passed | `cd testing/e2e && npx playwright test tests/member1_jaliya && npx playwright show-report ../reports/e2e/html` |
| 7 | `m1-e2e-step.png` | 10.1.5 | Jaliya | the ticket in the Support queue with the "Assistant tried first" badge | in that report: E2E-SUP-01 › Attachments, the step "staff see it in the Support queue…" |
| 8 | `m2-e2e-report.png` | 10.2.5 | Ranasinghe | report for his folder, all passed | as #6 with `tests/member2_ranasinghe` |
| 9 | `m2-e2e-step.png` | 10.2.5 | Ranasinghe | the report tracker moving to "handed in" | E2E-REG-03 › Attachments |
| 10 | `m3-e2e-report.png` | 10.3.5 | Uthpala | report for her folder, all passed | as #6 with `tests/member3_uthpala` |
| 11 | `m3-e2e-step.png` | 10.3.5 | Uthpala | the desk receiving an item by hand-in code | desk-handin spec › Attachments |
| 12 | `m4-e2e-report.png` | 10.4.5 | Braveena | report for her folder, all passed | as #6 with `tests/member4_braveena` |
| 13 | `m4-e2e-step.png` | 10.4.5 | Braveena | the collection code, then "Collected" | E2E-WF-01 › Attachments |
| 14 | `m1-security-newman.png` | 10.1.6 | Jaliya | Postman Collection Runner, folder "Member 1", every assertion passed | Postman desktop: Import `testing/security/foundu-security.postman_collection.json` and `local.postman_environment.json`, choose the environment, Run folder "Member 1" |
| 15 | `m1-security-sec28-429.png` | 10.1.6 | Jaliya | SEC-28 response **429 Too Many Requests**, Test Results tab all passed | in Postman, open SEC-28 and Send (right after #14, or send it twice) |
| 16 | `m2-security-newman.png` | 10.2.6 | Ranasinghe | Newman report for "Member 2", 0 failed | `testing/security/run-newman.sh m2 "Member 2"` then open `testing/reports/security/m2/newman-report.html` |
| 17 | `m3-security-newman.png` | 10.3.6 | Uthpala | same for "Member 3" | `testing/security/run-newman.sh m3 "Member 3"` |
| 18 | `m4-security-newman.png` | 10.4.6 | Braveena | same for "Member 4" | `testing/security/run-newman.sh m4 "Member 4"` |
| 19 | `m1-perf-k6.png` | 10.1.6 | Jaliya | k6 end-of-test summary, every threshold PASS | `cd testing/performance && k6 run member1_jaliya.js` (needs `FOUNDU_ADMIN_PASSWORD` set; run `clear` after setting it) |
| 20 | `m2-perf-k6.png` | 10.2.6 | Ranasinghe | same | `k6 run member2_ranasinghe.js` |
| 21 | `m3-perf-k6.png` | 10.3.6 | Uthpala | same | `k6 run member3_uthpala.js` |
| 22 | `m4-perf-k6.png` | 10.4.6 | Braveena | same | `k6 run member4_braveena.js` |
| 23 | `m1-a11y.png` | 10.1.6 | Jaliya | their accessibility tests passed (0 violations) | `cd testing/e2e && npx playwright test accessibility -g "Member 1 -" --reporter=list` |
| 24 | `m2-a11y.png` | 10.2.6 | Ranasinghe | same | `-g "Member 2 -"` |
| 25 | `m3-a11y.png` | 10.3.6 | Uthpala | same | `-g "Member 3 -"` |
| 26 | `m4-a11y.png` | 10.4.6 | Braveena | same | `-g "Member 4 -"` |
| 27 | `m1-mobile-tests.png` | 10.1.4 | Jaliya | flutter test run, "All tests passed!" | `cd mobile && flutter test test/member1_jaliya --reporter expanded` |
| 28 | `m2-mobile-tests.png` | 10.2.4 | Ranasinghe | same | `test/member2_ranasinghe` |
| 29 | `m3-mobile-tests.png` | 10.3.4 | Uthpala | same | `test/member3_uthpala` |
| 30 | `m4-mobile-tests.png` | 10.4.4 | Braveena | same | `test/member4_braveena` |
| 31 | `m1-mobile-screen.png` | 10.1.4 | Jaliya | emulator: the support chatbot (or Google sign-in) | app in the emulator, Help & support |
| 32 | `m2-mobile-screen.png` | 10.2.4 | Ranasinghe | emulator: report form with a photo | Report lost item |
| 33 | `m3-mobile-screen.png` | 10.3.4 | Uthpala | emulator: Found board or a possible-match score | Found tab |
| 34 | `m4-mobile-screen.png` | 10.4.4 | Braveena | emulator: answering verification questions | a claim with questions |
| 35 | `m1-summary.png` | 10.1.9 | Jaliya | final Summary table: every area listed, every count passed | `testing/member1_jaliya/run_tests.sh` (scroll to the Summary) |
| 36 | `m2-summary.png` | 10.2.9 | Ranasinghe | same | `testing/member2_ranasinghe/run_tests.sh` |
| 37 | `m3-summary.png` | 10.3.9 | Uthpala | same | `testing/member3_uthpala/run_tests.sh` |
| 38 | `m4-summary.png` | 10.4.9 | Braveena | same | `testing/member4_braveena/run_tests.sh` |
| 39 | `m1-agent-demo.png` | 10.1.7 | Jaliya | first report: input, plan, output, trace | `testing/member1_jaliya/agent_demo.sh --sample` |
| 40 | `m2-agent-demo.png` | 10.2.7 | Ranasinghe | same | `testing/member2_ranasinghe/agent_demo.sh --sample` |
| 41 | `m3-agent-demo.png` | 10.3.7 | Uthpala | same | `testing/member3_uthpala/agent_demo.sh --sample` |
| 42 | `m4-agent-demo.png` | 10.4.7 | Braveena | same | `testing/member4_braveena/agent_demo.sh --sample` |
| 43 | `m1-git-commits.png` | 10.1.9 | Jaliya | GitHub: their commits on `testing/QMSE-assignment` | GitHub › Commits › branch `testing/QMSE-assignment`, filter by author (after the push) |
| 44 | `m2-git-commits.png` | 10.2.9 | Ranasinghe | same | same |
| 45 | `m3-git-commits.png` | 10.3.9 | Uthpala | same | same |
| 46 | `m4-git-commits.png` | 10.4.9 | Braveena | same | same |

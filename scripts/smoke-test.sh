#!/usr/bin/env bash
# End-to-end smoke test for the StudentWebPortal API.
#
# Usage:   ./scripts/smoke-test.sh [base-url]        (default: http://localhost:5129)
# Needs:   bash + curl. Run against an EMPTY development database: it creates records
#          and deletes only the ones it can. Exit code is non-zero if any check fails.
#
# Runs on Linux/macOS, or on Windows via Git Bash / WSL.

BASE="${1:-http://localhost:5129}"
PASS=0; FAIL=0; BODY=""; HEADERS=""; STATUS=""

call() { # call METHOD PATH [JSON_BODY] [CONTENT_TYPE]
  local method="$1" path="$2" data="${3:-}" ctype="${4:-application/json}"
  local out; out="$(mktemp)"
  if [ -n "$data" ]; then
    STATUS=$(curl -s -m 15 -D "$out.h" -o "$out" -w '%{http_code}' -X "$method" "$BASE$path" -H "Content-Type: $ctype" -d "$data")
  else
    STATUS=$(curl -s -m 15 -D "$out.h" -o "$out" -w '%{http_code}' -X "$method" "$BASE$path")
  fi
  BODY="$(cat "$out")"; HEADERS="$(tr -d '\r' < "$out.h")"; rm -f "$out" "$out.h"
}
ok()   { PASS=$((PASS+1)); printf '  \033[32mPASS\033[0m %s\n' "$1"; }
bad()  { FAIL=$((FAIL+1)); printf '  \033[31mFAIL\033[0m %s\n         status=%s body=%s\n' "$1" "$STATUS" "${BODY:0:300}"; }
expect_status() { [ "$STATUS" = "$2" ] && ok "$1" || bad "$1 (expected HTTP $2)"; }
expect_body()   { case "$BODY" in *"$2"*) ok "$1";; *) bad "$1 (body should contain: $2)";; esac; }
expect_no_body(){ case "$BODY" in *"$2"*) bad "$1 (body should NOT contain: $2)";; *) ok "$1";; esac; }
expect_header() { echo "$HEADERS" | grep -qi "$2" && ok "$1" || bad "$1 (header should match: $2)"; }
id_of()         { echo "$BODY" | sed -n "s/.*\"$1\":\([0-9]*\).*/\1/p" | head -1; }

TODAY=$(date -u +%Y-%m-%d)
YESTERDAY=$(date -u -d 'yesterday' +%Y-%m-%d 2>/dev/null || date -u -v-1d +%Y-%m-%d)
TOMORROW=$(date -u -d 'tomorrow' +%Y-%m-%d 2>/dev/null || date -u -v+1d +%Y-%m-%d)

echo "Smoke testing $BASE"
echo "== Health"
call GET /health;                                   expect_status "GET /health is 200" 200; expect_body "reports Healthy" "Healthy"

echo "== Students"
call POST /api/Students '{"studentName":"  Farhan Ahmed ","email":"Farhan@Example.COM","phoneNumber":"98765 43210","enrollmentDate":"2026-01-10"}'
expect_status "create student" 201
expect_body   "email lower-cased" '"email":"farhan@example.com"'
expect_body   "local phone normalised to E.164" '"phoneNumber":"+919876543210"'
expect_body   "name trimmed" '"studentName":"Farhan Ahmed"'
S1=$(id_of studentId)
call POST /api/Students '{"studentName":"Phone Format","phoneNumber":"+91 (98765) 43211","enrollmentDate":"2026-01-10"}'
expect_status "formatted phone no longer causes HTTP 500" 201
S2=$(id_of studentId)
call POST /api/Students '{"studentName":"Bad Phone","phoneNumber":"123","enrollmentDate":"2026-01-10"}'
expect_status "invalid phone rejected" 400
call POST /api/Students '{"studentName":"Dup","email":"farhan@example.com","enrollmentDate":"2026-01-10"}'
expect_status "duplicate email is 409" 409; expect_header "409 is Problem Details" "application/problem+json"
call GET "/api/Students?search=AHMED";              expect_status "search (upper-case) 200" 200; expect_header "case-insensitive search finds 1" "X-Total-Count: 1"
call GET "/api/Students?search=%25";                expect_header "LIKE wildcard % is treated literally" "X-Total-Count: 0"
call GET "/api/Students?search=98765";              expect_header "search matches phone number" "X-Total-Count: 2"
call GET "/api/Students?pageSize=1&page=2";         expect_header "paging headers present" "X-Page: 2"
call GET "/api/Students?pageSize=500";              expect_status "oversized page rejected" 400; expect_header "paging error is Problem Details" "application/problem+json"
call GET "/api/Students/$S1";                       expect_status "get student by id" 200
call GET "/api/Students/999999";                    expect_status "unknown student is 404" 404
call PATCH "/api/Students/$S1" '[{"op":"replace","path":"/Notes","value":"Patched"}]' application/json-patch+json
expect_status "PATCH allowed field" 204
call PATCH "/api/Students/$S1" '[{"op":"replace","path":"/IsActive","value":false}]' application/json-patch+json
expect_status "PATCH forbidden field rejected" 400
call PATCH "/api/Students/$S1" '[{"op":"replace","path":"/PhoneNumber","value":"123"}]' application/json-patch+json
expect_status "PATCH invalid phone rejected" 400
call PATCH "/api/Students/$S1" '[{"op":"replace","path":"/PhoneNumber","value":"+91 (98765) 43210"}]' application/json-patch+json
expect_status "PATCH formatted phone accepted" 204
call PUT "/api/Students/$S1" '{"notes":"Updated via PUT"}'
expect_status "PUT partial update" 204
call GET "/api/Students/$S1/summary"
expect_status "summary for empty student" 200; expect_body "no attendance yet -> null rate" '"attendanceRatePercent":null'

echo "== Attendance"
call POST /api/Attendance "{\"studentId\":$S1,\"sessionStatus\":0,\"recordedBy\":0}"
expect_status "create today's attendance" 201; expect_body "response includes studentName" '"studentName":"Farhan Ahmed"'; expect_no_body "no leaked null 'student' object" '"student":'
A1=$(id_of id)
call POST /api/Attendance "{\"studentId\":$S1,\"sessionStatus\":0,\"recordedBy\":0}"
expect_status "duplicate day is 409" 409
call POST /api/Attendance "{\"studentId\":$S1,\"attendanceDate\":\"$YESTERDAY\",\"sessionStatus\":2,\"recordedBy\":1}"
expect_status "back-dated attendance allowed" 201
call POST /api/Attendance "{\"studentId\":$S1,\"attendanceDate\":\"$TOMORROW\",\"sessionStatus\":0,\"recordedBy\":0}"
expect_status "future attendance rejected" 400
call POST /api/Attendance '{"studentId":999999,"sessionStatus":0,"recordedBy":0}'
expect_status "unknown student rejected" 400
call POST /api/Attendance "{\"studentId\":$S1,\"sessionStatus\":9,\"recordedBy\":0}"
expect_status "invalid status rejected" 400
call PATCH "/api/Attendance/$A1" '[{"op":"replace","path":"/SessionStatus","value":1}]' application/json-patch+json
expect_status "PATCH attendance returns 200" 200; expect_body "returns DTO with studentName" '"studentName"'
call GET "/api/Attendance/student/$S1";             expect_status "list per student" 200; expect_header "2 records for student" "X-Total-Count: 2"

echo "== Hifz sessions (verse numbers are checked against the real surah length)"
call POST /api/HifzSession "{\"studentId\":$S1,\"sessionDate\":\"$TODAY\",\"surah\":1,\"verseStart\":7,\"status\":0,\"rank\":0,\"durationMinutes\":20,\"recordedBy\":0}"
expect_status "Al-Fatiha verse 7 accepted" 201; expect_body "has studentName" '"studentName":"Farhan Ahmed"'
H1=$(id_of id)
call POST /api/HifzSession "{\"studentId\":$S1,\"sessionDate\":\"$TODAY\",\"surah\":1,\"verseStart\":8,\"status\":0,\"rank\":0,\"durationMinutes\":20,\"recordedBy\":0}"
expect_status "Al-Fatiha verse 8 rejected" 400; expect_body "message names the verse count" "has 7 verses"
call POST /api/HifzSession "{\"studentId\":$S1,\"sessionDate\":\"$TODAY\",\"surah\":999,\"verseStart\":1,\"status\":0,\"rank\":0,\"durationMinutes\":20,\"recordedBy\":0}"
expect_status "invalid surah rejected" 400
call POST /api/HifzSession "{\"studentId\":$S1,\"sessionDate\":\"$TODAY\",\"surah\":2,\"verseStart\":286,\"status\":0,\"rank\":1,\"durationMinutes\":30,\"recordedBy\":1}"
expect_status "Al-Baqarah verse 286 accepted (last verse)" 201
call GET "/api/HifzSession/$H1";                    expect_status "get session" 200
call GET "/api/HifzSession?studentId=$S1&fromDate=$YESTERDAY&toDate=$TODAY"; expect_status "date-range filter" 200; expect_header "2 sessions in range" "X-Total-Count: 2"
call GET "/api/HifzSession?fromDate=$TODAY&toDate=$YESTERDAY"; expect_status "inverted date range rejected" 400; expect_header "Problem Details" "application/problem+json"
call PUT "/api/HifzSession/$H1" "{\"sessionDate\":\"$TODAY\",\"surah\":1,\"verseStart\":3,\"status\":2,\"rank\":2,\"durationMinutes\":15,\"recordedBy\":2}"
expect_status "update session" 204
call PUT "/api/HifzSession/$H1" "{\"sessionDate\":\"$TODAY\",\"surah\":1,\"verseStart\":99,\"status\":2,\"rank\":2,\"durationMinutes\":15,\"recordedBy\":2}"
expect_status "update with impossible verse rejected" 400

echo "== Revision sessions"
call POST /api/RevisionSession "{\"studentId\":$S1,\"sessionDate\":\"$TODAY\",\"surah\":2,\"verseStart\":1,\"verseEnd\":286,\"status\":0,\"rank\":0,\"durationMinutes\":45,\"recordedBy\":0}"
expect_status "full Al-Baqarah range accepted" 201
R1=$(id_of id)
call POST /api/RevisionSession "{\"studentId\":$S1,\"sessionDate\":\"$TODAY\",\"surah\":2,\"verseStart\":1,\"verseEnd\":287,\"status\":0,\"rank\":0,\"durationMinutes\":45,\"recordedBy\":0}"
expect_status "verseEnd beyond surah rejected" 400
call POST /api/RevisionSession "{\"studentId\":$S1,\"sessionDate\":\"$TODAY\",\"surah\":2,\"verseStart\":10,\"verseEnd\":5,\"status\":0,\"rank\":0,\"durationMinutes\":45,\"recordedBy\":0}"
expect_status "verseEnd before verseStart rejected" 400
call GET "/api/RevisionSession/$R1";                expect_status "get revision session" 200

echo "== Summary & integrity rules"
call GET "/api/Students/$S1/summary"
expect_status "summary 200" 200
expect_body "attendance counted" '"total":2'
expect_body "rate = (present+late)/total" '"attendanceRatePercent":50'
expect_body "hifz sessions counted" '"totalSessions":2'
expect_body "hifz minutes summed" '"totalMinutes":45'
call DELETE "/api/Students/$S1";                    expect_status "delete blocked while records exist" 409; expect_header "Problem Details" "application/problem+json"
call DELETE "/api/Students/$S2";                    expect_status "delete student with no records" 204
call DELETE "/api/Attendance/$A1";                  expect_status "delete attendance" 204
call GET "/api/Attendance/$A1";                     expect_status "deleted attendance is 404" 404
call DELETE "/api/HifzSession/$H1";                 expect_status "delete hifz session" 204
call DELETE "/api/RevisionSession/$R1";             expect_status "delete revision session" 204

echo
echo "Result: $PASS passed, $FAIL failed"
[ "$FAIL" -eq 0 ]

# StudentWebPortal — Review Preparation Notes

## What was verified (and how)

This pass was built and run, not just read:

- **Compiled** with the .NET 10.0.115 SDK: 0 errors, 0 warnings. The compile used the exact package DLLs from the `bin/` folder that came with the project (NuGet was not reachable from the review sandbox), so the package versions are the ones in `StudentWebPortal.csproj`. No packages were added or changed.
- **Migrations** applied cleanly to a fresh PostgreSQL 16 database, and EF reports no pending model changes (the model matches the migration snapshot).
- **Ran the API** against that database and executed `scripts/smoke-test.sh`: **67 / 67 checks pass**.
- **OpenAPI audit**: all 24 operations document a success response; create DTOs no longer expose server-controlled fields.
- **Quran table check**: 114 surahs, 6,236 verses in total, matching the known total.

Not verified: a Visual Studio / IIS Express run on your machine, the HTTPS profile, and the GitHub Actions workflow (it is a plain restore + build).

## Bugs found by running the original code, now fixed

| Problem (reproduced before fixing) | Fix |
|---|---|
| Name search was case-sensitive: searching `ahmed` returned nothing for "Farhan Ahmed" | `ILike` search, with LIKE wildcards escaped; also matches email and phone |
| A valid but formatted phone, e.g. `+91 (98765) 43210`, returned **HTTP 500** (longer than the 15-char column) | Phones are parsed and stored as E.164; numbers that can't fit are rejected with 400 |
| Phone numbers were stored exactly as typed (`98765 43210`) | Normalised to `+919876543210` on create, PUT and PATCH |
| Hifz session accepted verse 9999 of Al-Fatiha (7 verses) | Verse numbers validated against the real surah length (create and update, Hifz and Revision) |
| Delete-conflict returned `text/plain`; other errors were Problem Details | Every error is now `application/problem+json` |
| Create DTOs showed `student`, `createdAt`, `updatedAt`, `rowVersion` in Swagger | Removed (these are server-controlled) |
| Attendance responses leaked `"student": null` and the raw entity | New `AttendanceDto` with `studentName` |

## Improvements and additions

- `GET /api/Students/{id}/summary`: attendance rate, Hifz and revision totals, latest surah/verse.
- `GET /health`: checks database connectivity.
- Attendance can be back-dated (`attendanceDate`, optional); future dates are rejected. Previously it could only be recorded for "today".
- `ApiControllerBase` removes the copy-pasted paging, date-range, normalisation and unique-violation code from four controllers.
- Swagger: API title/description, documented success and error responses on every endpoint, and the site root redirects to Swagger in Development.
- Removed the unused `Status` enum.
- Added `scripts/smoke-test.sh`, an expanded `.http` file, a CI build workflow, and `.gitattributes` (keeps the shell script on LF line endings).
- README rewritten to match the code.

Earlier pass (kept): pagination and search, JSON Patch restricted to editable fields, 409 on duplicate email/attendance, delete blocked when records exist, UTC date handling, centralised audit fields and concurrency tokens, secrets removed from `appsettings.json`.

## Deliberately not added

Authentication/authorization, a frontend, Docker, a repository/service layer, and **new database migrations** (no schema change was needed). An xUnit project was also not added because packages could not be restored offline; the smoke test covers the same behaviour end to end.

## Five-minute demo script

1. Open the root URL: it redirects to Swagger. Show `/health`.
2. `POST /api/Students` with phone `98765 43210` and email `Farhan@Example.com`: the response shows `+919876543210` and a lower-cased email.
3. `GET /api/Students?search=AHMED`: case-insensitive search.
4. `POST /api/HifzSession` with `surah: 1`, `verseStart: 8`: 400 with "Verse 8 does not exist in AlFatiha; it has 7 verses."
5. `POST /api/Attendance` twice: 201 then 409. Then `GET /api/Students/{id}/summary`.
6. `DELETE /api/Students/{id}` while records exist: 409 Problem Details.

## Likely reviewer questions

- **Why no repository/service layer?** The logic is thin CRUD plus validation; an extra layer would be indirection without benefit. `ApiControllerBase` and DTOs keep controllers small. It becomes worth adding if business rules grow.
- **Why DTOs instead of entities?** Clients can't set server-controlled fields (timestamps, row version), and the API contract is stable if the schema changes.
- **How is concurrent editing handled?** PostgreSQL has no `rowversion`, so the app generates a `RowVersion` on every write. Session PUTs accept the version from a prior GET and return 409 if it's stale (verified).
- **Why block deleting students with records?** The foreign keys are `Restrict`, so history can't be orphaned; the API returns a clear 409 and the student can be set inactive instead.
- **What about security?** Not implemented, and documented as a limitation. The next step would be ASP.NET Core Identity / JWT with a Teacher role, and recording `RecordedBy` from the signed-in user instead of the request body.
- **How is it tested?** The smoke test exercises 67 scenarios against a real PostgreSQL database; the next step is xUnit + Testcontainers.

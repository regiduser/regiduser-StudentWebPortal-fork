# StudentWebPortal API

A REST API for managing student enrollment, attendance, Hifz (memorization) sessions, and revision sessions. Built with ASP.NET Core 10, Entity Framework Core, and PostgreSQL.


## Architecture

The project follows a small controller-and-data-access structure: controllers handle HTTP routing and response codes, DTOs define request/response shapes, FluentValidation and Data Annotations validate inputs, and `StudentWebPortalContext` owns EF Core persistence, relationships, and audit fields. This is intentionally kept lightweight; there is no separate repository/service layer because the current domain logic does not justify that extra indirection.

All controllers derive from `ApiControllerBase`, which provides the shared pieces: RFC 9457 Problem Details errors, pagination limits and headers, date-range checks, and input normalization.

## Features

- Student CRUD, case-insensitive search (name, email, phone), active-status filtering, and paginated listing
- **Student progress summary** (`/api/Students/{id}/summary`): attendance rate plus Hifz and revision totals and latest activity
- Attendance CRUD, per-student listing, optional back-dated records, and one record per student per date
- Hifz session CRUD with student/date/status filtering
- Revision session CRUD with student/date/status filtering and verse-range validation
- **Quran-aware validation**: verse numbers are checked against the real length of the selected surah (all 114 surahs, 6,236 verses)
- Phone numbers are validated with libphonenumber and stored in canonical E.164 form (`+919876543210`)
- Consistent Problem Details error responses (`application/problem+json`) for every 4xx
- DTO-based request/response handling and server-side validation
- PostgreSQL persistence with EF Core migrations
- `/health` endpoint that verifies database connectivity
- OpenAPI document and Swagger UI in Development (the site root redirects to Swagger)
- Audit timestamps and optimistic-concurrency tokens on attendance and learning sessions


## Technology stack

| Component | Technology |
|---|---|
| API | ASP.NET Core Web API (.NET 10) |
| ORM | Entity Framework Core 10 |
| Database | PostgreSQL / Npgsql |
| Validation | Data Annotations and FluentValidation |
| Phone handling | libphonenumber-csharp |
| API docs | OpenAPI and Swagger UI |
| JSON Patch | Newtonsoft.Json-based JSON Patch |


## Requirements

- .NET 10 SDK
- PostgreSQL 15+ (or a compatible supported PostgreSQL version)
- `dotnet-ef` CLI tool

Install/update the EF CLI:

```bash
dotnet tool install --global dotnet-ef
# or, if already installed
dotnet tool update --global dotnet-ef
```


## Setup

### 1. Clone and enter the project

```bash
git clone https://github.com/regiduser/StudentWebPortal.git
cd StudentWebPortal/StudentWebPortal
```

### 2. Configure the database connection without committing secrets

The committed appsettings contains a placeholder password only. Configure a local connection string using User Secrets:


```bash
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=quran_web_portal;Username=postgres;Password=YOUR_LOCAL_PASSWORD"
```

Alternatively, configure the environment variable `ConnectionStrings__DefaultConnection` for your environment. Never commit a real database password.

### 3. Apply migrations

From the `StudentWebPortal` project directory:

```bash
dotnet ef database update
```

### 4. Run the API

```bash
dotnet run
```

In Development, browse to the root URL: it redirects to Swagger UI. OpenAPI JSON is exposed at `/openapi/v1.json`. Check `/health` to confirm the API can reach the database.


## API endpoints

### Students

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/Students` | List students; supports `search`, `isActive`, `page`, `pageSize` |
| GET | `/api/Students/{id}` | Get a student by integer ID |
| GET | `/api/Students/{id}/summary` | Attendance rate, Hifz and revision totals, and latest activity |
| POST | `/api/Students` | Create a student |
| PUT | `/api/Students/{id}` | Update provided student fields (omitted fields are left unchanged) |
| PATCH | `/api/Students/{id}` | Apply JSON Patch to the supported editable fields |
| DELETE | `/api/Students/{id}` | Delete a student only when no linked records exist |

Example student listing (search is case-insensitive and matches name, email or phone):

```http
GET /api/Students?search=ahmed&isActive=true&page=1&pageSize=20
```

Example summary response:

```json
{
  "studentId": 1,
  "studentName": "Farhan Ahmed",
  "isActive": true,
  "attendance": { "total": 2, "present": 1, "late": 1, "absent": 0, "attendanceRatePercent": 100.0 },
  "hifz":       { "totalSessions": 2, "totalMinutes": 45, "lastSessionDate": "2026-11-19T00:00:00Z", "lastSurah": "AlBaqarah", "lastVerse": 286 },
  "revision":   { "totalSessions": 1, "totalMinutes": 45, "lastSessionDate": "2026-11-19T00:00:00Z", "lastSurah": "AlBaqarah", "lastVerse": 286 }
}
```

`attendanceRatePercent` counts Present and Late as attended, and is `null` until the first attendance record exists.

### Attendance

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/Attendance?page=1&pageSize=50` | List paginated attendance records |
| GET | `/api/Attendance/{id}` | Get an attendance record by record ID |
| GET | `/api/Attendance/student/{studentId}?page=1&pageSize=50` | List paginated records for a student |
| POST | `/api/Attendance` | Record attendance; `attendanceDate` is optional and defaults to today (UTC) |
| PUT | `/api/Attendance/{id}` | Update an attendance record |
| PATCH | `/api/Attendance/{id}` | Patch supported fields (`SessionStatus`, `Notes`, `RecordedBy`) |
| DELETE | `/api/Attendance/{id}` | Delete an attendance record |

Attendance is unique per student per date. Duplicate attempts return a conflict response, and future dates are rejected. Session and attendance dates are normalized to UTC when stored.

### Hifz sessions

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/HifzSession` | List/filter/paginate Hifz sessions (`studentId`, `status`, `fromDate`, `toDate`, `page`, `pageSize`) |
| GET | `/api/HifzSession/{id}` | Get a session |
| POST | `/api/HifzSession` | Create a session for an existing student |
| PUT | `/api/HifzSession/{id}` | Update a session (optionally send `rowVersion` for concurrency protection) |
| DELETE | `/api/HifzSession/{id}` | Delete a session |

### Revision sessions

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/RevisionSession` | List/filter/paginate revision sessions (`studentId`, `status`, `fromDate`, `toDate`, `page`, `pageSize`) |
| GET | `/api/RevisionSession/{id}` | Get a session |
| POST | `/api/RevisionSession` | Create a revision session for an existing student |
| PUT | `/api/RevisionSession/{id}` | Update a revision session (optionally send `rowVersion`) |
| DELETE | `/api/RevisionSession/{id}` | Delete a revision session |

### Operations

| Method | Endpoint | Description |
|---|---|---|
| GET | `/health` | `Healthy` when the database is reachable, `Unhealthy` otherwise |


## Data rules

- **Verses must exist.** The selected surah determines the allowed range: Al-Fatiha accepts verses 1 to 7, Al-Baqarah 1 to 286, and so on. Revision sessions also require `verseEnd >= verseStart`.
- **Phone numbers** are parsed with region `IN` as the default. A valid number in any common format (`98765 43210`, `+91 (98765) 43210`) is stored as `+919876543210`; an invalid number is rejected with 400.
- **Emails** are lower-cased and must be unique (case-insensitive in effect), otherwise 409.
- **Deleting a student** is blocked while attendance or session records reference them; mark the student inactive instead.


## Response behavior

- `200 OK` – successful retrieval or PATCH response
- `201 Created` – successful creation, with a resource location
- `204 No Content` – successful update or deletion
- `400 Bad Request` – validation failure, invalid paging/date range, or referenced student does not exist
- `404 Not Found` – requested record does not exist
- `409 Conflict` – duplicate attendance/email, stale `rowVersion`, or a student has linked records that prevent deletion

Every error body is Problem Details (`application/problem+json`); validation failures include an `errors` map keyed by field name.


## Testing

`scripts/smoke-test.sh` is an end-to-end test of the running API (67 checks covering CRUD, validation rules, conflicts, paging, search, and the summary endpoint). It needs only bash and curl (Git Bash or WSL on Windows).

```bash
# terminal 1: start the API against an EMPTY development database
cd StudentWebPortal && dotnet run

# terminal 2: from the repository root
./scripts/smoke-test.sh http://localhost:5129
```

It prints PASS/FAIL per check and exits non-zero if anything fails. `StudentWebPortal/StudentWebPortal.http` contains the same requests for manual use in Visual Studio / VS Code.


## Notes and known limitations

- Authentication and authorization are not implemented; this API is not intended to be exposed publicly without additional security controls.
- Student PUT applies non-null fields only; PATCH is the way to clear nullable values.
- List endpoints return arrays and pagination metadata through `X-Total-Count`, `X-Page`, and `X-Page-Size` headers rather than a response envelope.
- There is no xUnit/integration test project yet; the smoke test above is the automated check. A good next step is integration tests using Testcontainers for PostgreSQL.
- `FluentValidation.AspNetCore` (automatic validation) is deprecated upstream. It works with the current setup, but a future cleanup should replace it with a small validation filter or endpoint filter.
- Optimistic concurrency uses an application-generated `RowVersion` (PostgreSQL has no `rowversion` type). Sessions honor a client-supplied `rowVersion`; attendance guards against races within a request.


## Repository layout

```text
StudentWebPortal/
├── Common/           # Quran verse metadata, phone normalization
├── Controllers/      # ApiControllerBase + one controller per resource
├── Data/             # EF Core DbContext
├── Health/           # /health database check
├── Migrations/
├── Model/
│   ├── Dto/
│   └── Entity/
├── Validators/
├── Program.cs
├── appsettings.json
└── StudentWebPortal.csproj
scripts/smoke-test.sh # end-to-end API test
.github/workflows/    # CI build
```

## License

MIT. See [LICENSE](LICENSE).

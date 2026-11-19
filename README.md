# StudentWebPortal API

A REST API for managing student enrollment, attendance, Hifz (memorization) sessions, and revision sessions. Built with ASP.NET Core 10, Entity Framework Core, and PostgreSQL.


## Architecture

The project follows a small controller-and-data-access structure: controllers handle HTTP routing and response codes, DTOs define request/response shapes, FluentValidation and Data Annotations validate inputs, and `StudentWebPortalContext` owns EF Core persistence, relationships, and audit fields. This is intentionally kept lightweight; there is no separate repository/service layer because the current domain logic does not justify that extra indirection.

## Features

- Student CRUD, search, active-status filtering, and paginated listing
- Attendance CRUD, per-student listing, and one attendance record per student per date
- Hifz session CRUD with student/date/status filtering
- Revision session CRUD with student/date/status filtering and verse-range validation
- DTO-based request/response handling and server-side validation
- PostgreSQL persistence with EF Core migrations
- OpenAPI endpoint and Swagger UI in Development
- Audit timestamps and concurrency-token support on attendance and learning sessions


## Technology stack

| Component | Technology |
|---|---|
| API | ASP.NET Core Web API (.NET 10) |
| ORM | Entity Framework Core 10 |
| Database | PostgreSQL / Npgsql |
| Validation | Data Annotations and FluentValidation |
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

Swagger UI is enabled in the Development environment at the Swagger URL shown in the console (typically `/swagger`). OpenAPI JSON is exposed at `/openapi/v1.json` in Development.


## API endpoints

### Students

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/Students` | List students; supports `search`, `isActive`, `page`, `pageSize` |
| GET | `/api/Students/{id}` | Get a student by integer ID |
| POST | `/api/Students` | Create a student |
| PUT | `/api/Students/{id}` | Update provided student fields (legacy partial-update behavior) |
| PATCH | `/api/Students/{id}` | Apply JSON Patch to the supported editable fields |
| DELETE | `/api/Students/{id}` | Delete a student only when no linked records exist |

Example student listing:

```http
GET /api/Students?search=far&isActive=true&page=1&pageSize=20
```

### Attendance

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/Attendance?page=1&pageSize=50` | List paginated attendance records |
| GET | `/api/Attendance/{id}` | Get an attendance record by record ID |
| GET | `/api/Attendance/student/{studentId}?page=1&pageSize=50` | List paginated records for a student |
| POST | `/api/Attendance` | Create today's attendance record |
| PUT | `/api/Attendance/{id}` | Update an attendance record |
| PATCH | `/api/Attendance/{id}` | Patch supported fields (`SessionStatus`, `Notes`, `RecordedBy`) |
| DELETE | `/api/Attendance/{id}` | Delete an attendance record |

Attendance is unique per student per date. Duplicate attempts return a conflict response. Session and attendance dates are normalized to UTC when stored.

### Hifz sessions

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/HifzSession` | List/filter/paginate Hifz sessions (`studentId`, `status`, `fromDate`, `toDate`, `page`, `pageSize`) |
| GET | `/api/HifzSession/{id}` | Get a session |
| POST | `/api/HifzSession` | Create a session for an existing student |
| PUT | `/api/HifzSession/{id}` | Update a session |
| DELETE | `/api/HifzSession/{id}` | Delete a session |

### Revision sessions

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/RevisionSession` | List/filter/paginate revision sessions (`studentId`, `status`, `fromDate`, `toDate`, `page`, `pageSize`) |
| GET | `/api/RevisionSession/{id}` | Get a session |
| POST | `/api/RevisionSession` | Create a revision session for an existing student |
| PUT | `/api/RevisionSession/{id}` | Update a revision session |
| DELETE | `/api/RevisionSession/{id}` | Delete a revision session |

Revision verse ranges are validated so the end verse cannot precede the start verse.


## Response behavior

- `200 OK` – successful retrieval or PATCH response
- `201 Created` – successful creation, with a resource location
- `204 No Content` – successful update or deletion
- `400 Bad Request` – invalid request or referenced student does not exist
- `404 Not Found` – requested record does not exist
- `409 Conflict` – duplicate attendance/email or a student has linked records that prevent deletion

Validation failures are automatically returned by the API controller model-validation pipeline.


## Notes and known limitations

- Authentication and authorization are not implemented; this API is not intended to be exposed publicly without additional security controls.
- Student PUT currently preserves the project's previous behavior of applying non-null fields only; PATCH is the preferred way to express partial updates and clear nullable values.
- List endpoints return arrays and pagination metadata through `X-Total-Count`, `X-Page`, and `X-Page-Size` headers rather than a response envelope.
- No automated test project was included in the source archive. Run manual endpoint tests through Swagger or add integration tests before production use.
- Concurrency token behavior should be verified against the configured PostgreSQL schema and EF Core migrations before relying on concurrent-edit protection in production.


## Repository layout

```text
StudentWebPortal/
├── Controllers/
├── Data/
├── Migrations/
├── Model/
│   ├── Dto/
│   └── Entity/
├── Validators/
├── Program.cs
├── appsettings.json
└── StudentWebPortal.csproj
```

## License

MIT. See [LICENSE](LICENSE).

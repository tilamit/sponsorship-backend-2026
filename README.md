# Sponsorship Request Approval Workflow - Backend

ASP.NET Core 8 Web API for an internal sponsorship request approval workflow. Staff submit requests, a Manager approves first, a Finance Admin gives final approval, and a System Admin oversees everything. Built on Onion Architecture with EF Core, JWT + rotating refresh tokens, Serilog, and Swagger.

---

## 1. Tech Stack

| Layer            | Technology                                                |
| ---------------- | --------------------------------------------------------- |
| Runtime          | .NET 8.0 (LTS)                                            |
| Web              | ASP.NET Core Web API                                      |
| ORM              | Entity Framework Core 8 (Code-First + Migrations)         |
| Database         | Microsoft SQL Server (LocalDB for dev, MSSQL for prod)    |
| Auth             | JWT access token + opaque rotating refresh token (cookie) |
| Password hashing | BCrypt.Net-Next                                           |
| Validation       | FluentValidation                                          |
| Logging          | Serilog (console + rolling file sinks)                    |
| API Docs         | Swashbuckle (Swagger UI with JWT Bearer support)          |
| Architecture     | Onion (Domain -> Application -> Infrastructure -> Api)    |
| Rate limiting    | Built-in .NET 8 Rate Limiter (sliding/fixed/token bucket) |

---

## 2. Solution Layout

```
Backend/
  Sponsorship.sln
  global.json                              SDK pin (8.0.416)
  NuGet.config
  db/
    01_schema.sql                          Idempotent schema (EF script)
    02_seed.sql                            Roles, sponsorship types, demo users
    README.md                              DB setup notes
  src/
    Sponsorship.Domain/                    Entities, enums, domain exceptions
    Sponsorship.Application/               Services, DTOs, validators, ports
    Sponsorship.Infrastructure/            EF Core, JWT, BCrypt, repositories
    Sponsorship.Api/                       Composition root, controllers, middleware
```

### 2.1 Project Responsibilities

- Sponsorship.Domain - pure C#, zero external dependencies. Holds entities (User, Role, SponsorshipRequest, SponsorshipType, WorkflowHistory, RefreshToken), enums (RequestStatus, WorkflowAction), and domain exceptions. No EF Core attributes here.
- Sponsorship.Application - persistence-agnostic business logic. Defines ports (IUserRepository, ISponsorshipRequestRepository, IUnitOfWork, IJwtTokenService, IPasswordHasher, ICurrentUserService, ICacheService, IDateTimeProvider), DTOs, services (AuthService, SponsorshipRequestService, WorkflowService, SponsorshipTypeService), and FluentValidation validators.
- Sponsorship.Infrastructure - EF Core implementation (AppDbContext, Fluent API configurations, repositories, UnitOfWork, DbSeeder), JwtTokenService, PasswordHasher, and MemoryCacheService. References Application and Domain only.
- Sponsorship.Api - the only project that references Infrastructure. Wires DI in Program.cs, exposes controllers, hosts Swagger, applies CORS, rate limiting, exception middleware, and Serilog request logging.

### 2.2 Onion Dependency Rules

- Domain has zero project references.
- Application references Domain only.
- Infrastructure references Application and Domain. Never the Api.
- Api is the composition root and references all three.
- Controllers depend on Application interfaces, never on AppDbContext directly.

---

## 3. Business Domain

### 3.1 Roles

| Role          | Responsibility                                                       |
| ------------- | -------------------------------------------------------------------- |
| Requestor     | Create, edit drafts, submit, cancel own requests                     |
| Manager       | First-level approve/reject                                           |
| FinanceAdmin  | Final approve/reject                                                 |
| SystemAdmin   | Read all requests and history, manage sponsorship types              |

### 3.2 Workflow State Machine

```
            (none) --create--> Draft
            Draft  --submit--> PendingManagerApproval
            Draft  --edit-->   Draft
            Draft / Pending* --cancel (owner)--> Cancelled

            PendingManagerApproval --approve (Manager)--> PendingFinanceReview
            PendingManagerApproval --reject  (Manager)--> Rejected

            PendingFinanceReview   --approve (FinanceAdmin)--> Approved
            PendingFinanceReview   --reject  (FinanceAdmin)--> Rejected
```

Status enum: Draft, PendingManagerApproval, PendingFinanceReview, Approved, Rejected, Cancelled.

Any transition not in the table returns 400 with a clear error. The state machine lives in the Application layer (WorkflowService / SponsorshipRequestService) so every transition is enforced regardless of which endpoint triggered it.

### 3.3 Request Fields

Required:

- Title (max 200)
- RequestorId (auto-filled from JWT, not accepted from the body)
- Department (max 100)
- SponsorshipTypeId (FK to SponsorshipTypes lookup)
- EventName (max 200)
- EventDate (must be >= today on submit)
- RequestedAmount (decimal(18,2), greater than 0)
- Purpose (max 2000)

Optional:

- ExpectedBenefit (max 1000)
- Remarks (max 500)
- SupportingDocumentPath (file upload deferred from assessment scope)

---

## 4. RBAC and Authorization

### 4.1 How role checks work

- The JWT contains a role claim derived from User.Role.Name.
- Controllers use `[Authorize(Roles = "...")]` for coarse role gating.
- Ownership and workflow-state rules live in the Application services. For example, a Requestor can only cancel their own request - the service compares ICurrentUserService.UserId against request.RequestorId and throws ForbiddenException otherwise.

### 4.2 Endpoint role matrix

| Endpoint                                              | Allowed roles                       |
| ----------------------------------------------------- | ----------------------------------- |
| POST /api/auth/login                                  | Anonymous                           |
| POST /api/auth/refresh                                | Anonymous (refresh cookie required) |
| POST /api/auth/logout                                 | Anonymous (refresh cookie required) |
| GET  /api/users/me                                    | Any authenticated user              |
| GET  /api/sponsorship-types                           | Any authenticated user              |
| POST /api/sponsorship-types                           | SystemAdmin                         |
| PUT  /api/sponsorship-types/{id}                      | SystemAdmin                         |
| GET  /api/sponsorship-requests                        | Any (filtered by role in service)   |
| GET  /api/sponsorship-requests/{id}                   | Any (ownership/role enforced)       |
| POST /api/sponsorship-requests                        | Requestor                           |
| PUT  /api/sponsorship-requests/{id}                   | Requestor (own draft)               |
| POST /api/sponsorship-requests/{id}/submit            | Requestor (own draft)               |
| POST /api/sponsorship-requests/{id}/cancel            | Requestor (own, not yet Approved)   |
| GET  /api/workflow/pending-manager                    | Manager, SystemAdmin                |
| GET  /api/workflow/pending-finance                    | FinanceAdmin, SystemAdmin           |
| POST /api/workflow/{id}/manager-decision              | Manager                             |
| POST /api/workflow/{id}/finance-decision              | FinanceAdmin                        |
| GET  /api/workflow/{id}/history                       | Any (visibility filtered)           |
| GET  /api/health                                      | Anonymous                           |

Every approval and rejection writes a WorkflowHistory row capturing actor, action, from/to status, remarks, and UTC timestamp, so the audit trail is complete and queryable.

---

## 5. Authentication Model (JWT + Rotating Refresh Token)

### 5.1 Two tokens

- Access token - short-lived JWT (15 minutes), signed HS256, carries sub (user id), email, role. Returned in the JSON body of login/refresh responses and sent back as `Authorization: Bearer <token>`.
- Refresh token - long-lived (7 days), opaque random string, stored server-side in the RefreshTokens table, and returned to the client only as an HttpOnly, Secure, SameSite=Strict cookie scoped to `/api/auth`. The client never reads it from JS, and it is never persisted to localStorage.

### 5.2 Endpoints

- POST /api/auth/login - body { email, password }. On success sets the refresh cookie and returns { accessToken, accessTokenExpiresAt, refreshTokenExpiresAt, user }.
- POST /api/auth/refresh - no body. Reads the refresh cookie, validates it against the DB (exists, not revoked, not expired), issues a new pair, rotates the cookie. Returns the new access token in the body.
- POST /api/auth/logout - revokes the refresh token from the cookie and clears the cookie.

### 5.3 Rotation and theft detection

1. Every refresh issues a brand-new refresh token. The old row is marked Revoked and gets ReplacedByToken set to the new token.
2. If a client presents a refresh token that is already revoked, the whole chain for that user is revoked. This is the classic reuse-detection signal: it means either the old client kept replaying an old token (bug) or someone stole it (attack). Either way the user is forced to log in again.
3. Refresh validation does not look at the access token at all - the refresh cookie alone is the credential. The access token is stateless and only checked by the JWT middleware on every request.

### 5.4 Configuration

```json
"Jwt": {
  "Issuer": "SponsorshipApi",
  "Audience": "SponsorshipClient",
  "Key": "REPLACE_WITH_A_LONG_RANDOM_SECRET_AT_LEAST_32_CHARS",
  "AccessTokenMinutes": 15,
  "RefreshTokenDays": 7
}
```

In production override `Jwt:Key` and `ConnectionStrings:DefaultConnection` with environment variables (`Jwt__Key`, `ConnectionStrings__DefaultConnection`) - never commit secrets.

---

## 6. Database

### 6.1 Tables

```
Users                Id (uniqueidentifier PK), Email (unique), FullName, Department,
                     PasswordHash, RoleId (FK), IsActive, CreatedAt
Roles                Id (int PK), Name (unique)
SponsorshipTypes     Id (int PK), Name (unique), IsActive
SponsorshipRequests  Id (uniqueidentifier PK), Title, RequestorId (FK), Department,
                     SponsorshipTypeId (FK), EventName, EventDate, RequestedAmount,
                     Purpose, ExpectedBenefit, Remarks, Status (int enum),
                     SupportingDocumentPath, CreatedAt, UpdatedAt
WorkflowHistory      Id (bigint PK), RequestId (FK), ActionById (FK), Action (int enum),
                     FromStatus (int enum), ToStatus (int enum), Remarks, ActionAt
RefreshTokens        Id (uniqueidentifier PK), UserId (FK), Token (unique),
                     ExpiresAt, CreatedAt, RevokedAt, ReplacedByToken
```

Indexes: IX_SponsorshipRequests_RequestorId, IX_SponsorshipRequests_Status, IX_WorkflowHistory_RequestId, IX_RefreshTokens_Token (unique).

### 6.2 Setup - Option A: EF Core Migrations (recommended for development)

```powershell
# From Backend/ folder
dotnet ef database update `
  --project src/Sponsorship.Infrastructure `
  --startup-project src/Sponsorship.Api
```

On first startup, `DbSeeder` runs and inserts:

- 4 roles: Requestor, Manager, FinanceAdmin, SystemAdmin
- 5 sponsorship types: Event, Charity, Sports, Education, CommunityOutreach
- 4 demo users (passwords all `Demo@123`)

The seeder is idempotent - it checks for existing rows before inserting.

### 6.3 Setup - Option B: Raw SQL via SSMS

1. Open SQL Server Management Studio 2022.
2. Connect to `(localdb)\MSSQLLocalDB` or your remote SQL Server.
3. Create an empty database: `CREATE DATABASE SponsorshipDb;`
4. Execute `db/01_schema.sql` against `SponsorshipDb`.
5. Execute `db/02_seed.sql` against `SponsorshipDb`.

`db/02_seed.sql` contains a pre-baked BCrypt hash for `Demo@123` and uses `IF NOT EXISTS` guards so it can be re-run safely.

### 6.4 Regenerating the schema script

After adding a new migration:

```powershell
dotnet ef migrations script --idempotent `
  --project src/Sponsorship.Infrastructure `
  --startup-project src/Sponsorship.Api `
  --output db/01_schema.sql
```

### 6.5 Connection String

`appsettings.Development.json` should point to LocalDB:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=SponsorshipDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
}
```

For the live test environment a remote MSSQL connection string is used (override with the `ConnectionStrings__DefaultConnection` env var, do not commit it).

---

## 7. Local Setup

### 7.1 Prerequisites

- .NET 8 SDK (pinned to 8.0.416 in `global.json`, latestFeature roll-forward enabled)
- SQL Server LocalDB or a reachable SQL Server instance
- Visual Studio 2026 (recommended) or `dotnet` CLI
- SQL Server Management Studio 2022 (optional, for SQL inspection)
- `dotnet-ef` CLI: `dotnet tool install --global dotnet-ef --version 8.*`

### 7.2 Run the API

```powershell
# From Backend/
dotnet restore
dotnet ef database update --project src/Sponsorship.Infrastructure --startup-project src/Sponsorship.Api
dotnet run --project src/Sponsorship.Api
```

The API listens on:

- https://localhost:7225
- http://localhost:5130

Swagger UI is available at https://localhost:7225/swagger.

### 7.3 Demo Accounts

| Email                  | Password   | Role          |
| ---------------------- | ---------- | ------------- |
| requestor@demo.local   | Demo@123   | Requestor     |
| manager@demo.local     | Demo@123   | Manager       |
| finance@demo.local     | Demo@123   | FinanceAdmin  |
| admin@demo.local       | Demo@123   | SystemAdmin   |

### 7.4 Smoke Test in Swagger

1. Open Swagger UI.
2. POST /api/auth/login as the requestor. Copy the accessToken from the response body.
3. Click Authorize at the top of Swagger and paste the token (no `Bearer ` prefix).
4. POST /api/sponsorship-requests to create a draft.
5. POST /api/sponsorship-requests/{id}/submit to push it to PendingManagerApproval.
6. Log in as the manager, GET /api/workflow/pending-manager, then POST /api/workflow/{id}/manager-decision with `{ "action": "Approve" }`.
7. Log in as finance, GET /api/workflow/pending-finance, then POST /api/workflow/{id}/finance-decision with `{ "action": "Approve" }`.
8. GET /api/workflow/{id}/history to see the full audit trail.

---

## 8. Deployment - Visual Studio Dev Tunnel

The backend is exposed publicly using Visual Studio's built-in Dev Tunnels feature. No external host or paid plan is involved. The tunnel forwards traffic to the locally running Kestrel instance over HTTPS with a Microsoft-managed certificate.

### 8.1 One-time setup

1. Open `Sponsorship.sln` in Visual Studio 2026.
2. Sign in to Visual Studio with the same Microsoft account you want associated with the tunnel.
3. From the toolbar dropdown next to the Run button choose `Dev Tunnels` -> `Create a Tunnel...`.
4. In the dialog:
   - Account - the signed-in account
   - Name - e.g. `sponsorship-api`
   - Tunnel Type - `Persistent` (URL stays the same across restarts)
   - Access - `Public` (the assessor needs to reach it without an MS account)
5. Click OK. Visual Studio creates the tunnel and selects it as active.

### 8.2 Run with the tunnel

1. Make sure the `https` launch profile is selected.
2. Press F5 (or Ctrl+F5). Kestrel starts on https://localhost:7225 and the tunnel UI shows the public URL (something like `https://abcd1234-7225.use.devtunnels.ms`).
3. Open the public URL + `/swagger` to confirm it is reachable.
4. The tunnel host (`*.devtunnels.ms`) must also be in the `Cors:AllowedOrigins` list if the Angular frontend will hit it from a different origin in the browser - otherwise CORS preflights will fail.

### 8.3 Database for the deployed instance

For the live test environment the API is configured against a remote MSSQL database (see `appsettings.json`). Apply the schema once:

- Connect SSMS to the remote MSSQL host with the same credentials.
- Run `db/01_schema.sql` then `db/02_seed.sql`.

Alternatively run `dotnet ef database update` locally with the production connection string in the `ConnectionStrings__DefaultConnection` environment variable.

### 8.4 Caveats

- The tunnel stays up only while Visual Studio (and the app) is running. Close VS and the public URL stops responding.
- First request after the tunnel has been idle can be slightly slower because the connection is being re-established.
- Persistent tunnels keep the same URL across restarts, but the tunnel must be re-launched from VS after a reboot.
- Do not commit the tunnel URL into source code. Keep it in the README / submission notes only and inject it into the frontend's `environment.prod.ts` at build time.

### 8.5 Project URL

- API: https://x1xlffq8-7225.inc1.devtunnels.ms/swagger/index.html

---

## 9. Cross-Cutting Concerns

### 9.1 Logging - Serilog

Console sink + daily rolling file sink at `logs/app-YYYYMMDD.log`, 7 days retained. EF Core and ASP.NET host noise is suppressed to Warning. Request logging is enabled via `UseSerilogRequestLogging`.

### 9.2 Error Handling

`ExceptionHandlingMiddleware` converts exceptions to RFC 7807 ProblemDetails responses:

- ValidationException (FluentValidation) -> 400 with field-level errors
- InvalidWorkflowTransitionException / DomainException -> 400 with message
- NotFoundException -> 404
- ForbiddenException -> 403
- UnauthorizedException -> 401
- Anything else -> 500 with a generic message (full exception is logged server-side)

### 9.3 Validation

FluentValidation runs explicitly in each controller action via `ValidateAndThrowAsync`. Validators live in the Application layer and are auto-registered from the assembly. This keeps the controller body small and the rules unit-testable independently from MVC.

### 9.4 Rate Limiting

Configured in `RateLimitExtensions.AddSponsorshipRateLimiting`:

- Global - sliding window, 100 requests per minute per authenticated user (or per IP if anonymous).
- `auth` policy on the auth controller - fixed window, 5 requests per minute per IP for brute-force protection on login/refresh.
- `writes` policy on all create/update/workflow-action endpoints - token bucket per user, 30 tokens cap, refill 10 every 10 seconds.

Rejections return 429 with a ProblemDetails body and a Retry-After header.

### 9.5 Caching

`ICacheService` is backed by `IMemoryCache` (single-node in-process). Currently used for `/api/users/me` with a 2-minute TTL keyed by user id. The cache has a size limit of 1024 entries and 20 percent compaction to avoid unbounded growth.

### 9.6 CORS

Origins are read from `Cors:AllowedOrigins` in configuration. The Angular dev URL `http://localhost:4200` is allowed in Development; production hosts and the dev tunnel URL must be added to the list and the API restarted. The policy is named `AllowFrontend`, restricts headers to `Content-Type` and `Authorization`, allows the four HTTP verbs in use, and enables credentials so the refresh cookie can flow.

### 9.7 Compression

Brotli and Gzip response compression is enabled for JSON, including over HTTPS, at the Fastest level - good default trade-off for an API.

### 9.8 Cookie Policy

Refresh-token cookies are set with HttpOnly = true, Secure = true, SameSite = Strict, Path = /api/auth. This means JS on the SPA cannot read them, browsers will not send them over plain HTTP, and they will not be attached to third-party requests.

---

## 10. API Surface Reference

See Swagger for the full schema with examples. Quick reference:

```
POST   /api/auth/login                                 Login (returns access token + refresh cookie)
POST   /api/auth/refresh                               Rotate tokens (refresh cookie required)
POST   /api/auth/logout                                Revoke refresh token, clear cookie

GET    /api/users/me                                   Current user profile

GET    /api/sponsorship-types?activeOnly=true          List sponsorship types
POST   /api/sponsorship-types                          Create (SystemAdmin)
PUT    /api/sponsorship-types/{id}                     Update (SystemAdmin)

GET    /api/sponsorship-requests                       List (scoped by role)
GET    /api/sponsorship-requests/{id}                  Get one (scoped by role)
POST   /api/sponsorship-requests                       Create draft (Requestor)
PUT    /api/sponsorship-requests/{id}                  Edit own draft (Requestor)
POST   /api/sponsorship-requests/{id}/submit           Draft -> PendingManagerApproval
POST   /api/sponsorship-requests/{id}/cancel           Owner cancel

GET    /api/workflow/pending-manager                   Manager queue
GET    /api/workflow/pending-finance                   Finance queue
POST   /api/workflow/{id}/manager-decision             { action: Approve|Reject, remarks }
POST   /api/workflow/{id}/finance-decision             { action: Approve|Reject, remarks }
GET    /api/workflow/{id}/history                      Audit trail for one request

GET    /api/health                                     Liveness probe
```

---

## 11. Design Decisions and Tradeoffs

- Refresh tokens are stored as raw strings, not hashes. Acceptable for assessment scope; in production they would be hashed at rest the same way passwords are. The HttpOnly cookie still protects them from XSS exfiltration.
- No CQRS / MediatR. Service classes are clearer and avoid an extra dependency without losing testability.
- Manual DTO mapping (no AutoMapper). The mapping surface is small enough that the convenience of a mapper does not outweigh the runtime reflection cost and the indirection.
- IUnitOfWork wraps EF Core's SaveChanges. Repositories never call SaveChanges directly - that responsibility belongs to the service so transactional boundaries are explicit.
- File upload (SupportingDocumentPath) is in the schema but the upload endpoint is intentionally not implemented - the column is reserved for a future iteration.
- In-memory cache (IMemoryCache) is fine for single-instance deployments. A multi-node deployment would swap `MemoryCacheService` for a distributed cache (Redis) by re-binding `ICacheService`.
- Swagger is left enabled in all environments so the assessor can drive the API directly. In a real production deployment it would be Development-only.

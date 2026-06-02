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
| Transactions     | Serializable DB transactions via IUnitOfWork + EF execution strategy |

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
    Sponsorship.Application.Tests/         xUnit test suite (see section 12)
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

## 8. Deployment - SmarterASP.NET Hosting

The backend is hosted on **SmarterASP.NET** (free Windows/IIS plan), which natively runs .NET 8 and provides a free MSSQL database plus a temporary `*.ntempurl.com` HTTPS URL. Unlike the previous dev-tunnel setup, the app stays online without Visual Studio running.

### 8.1 One-time account and site setup

1. Sign up for a free account at https://www.smarterasp.net/ (no credit card).
2. In the control panel create a **website** - you get a temporary URL like `https://atbest2020-001-site1.ntempurl.com`. Confirm the app pool is set to **.NET Core / .NET 8**.
3. Create an **MSSQL database** from the control panel. Note the server host, database name, user, and password - these go into the live connection string.

### 8.2 Publish from Visual Studio

1. In the control panel open **Websites -> (your site) -> Publish Settings** and download the `.PublishSettings` file (Web Deploy).
2. In Visual Studio 2026 right-click `Sponsorship.Api` -> **Publish** -> **Import Profile** and select the downloaded file.
3. Click **Publish**. Web Deploy pushes the build to IIS; the site is live at the `*.ntempurl.com` URL.

> FTP alternative: the control panel also exposes FTP credentials. Publish to a local folder (`dotnet publish -c Release`) and upload the output to the site's `wwwroot` if you prefer not to use Web Deploy.

### 8.3 Production configuration (secrets)

Do not commit secrets. Override them on the host instead - set them as **App Settings / Environment Variables** in the SmarterASP.NET control panel (or in the deployed `appsettings.Production.json`):

- `ConnectionStrings__DefaultConnection` - the remote MSSQL connection string from step 8.1.
- `Jwt__Key` - a long random secret (>= 32 chars).
- `ASPNETCORE_ENVIRONMENT` - kept as `Development` (or Swagger left on) so assessors can drive the API.

### 8.4 Apply the database schema

Once the MSSQL database exists, seed it once:

- Connect **SSMS 2022** to the remote MSSQL host using the credentials from step 8.1, then run `db/01_schema.sql` followed by `db/02_seed.sql`.
- Alternatively run `dotnet ef database update` locally with the remote connection string in the `ConnectionStrings__DefaultConnection` environment variable.

### 8.5 CORS

Add the deployed frontend origin (and the `*.ntempurl.com` API URL if hit cross-origin) to `Cors:AllowedOrigins`, otherwise browser preflight requests fail. The policy already enables credentials so the refresh cookie can flow.

### 8.6 Caveats

- The free plan sleeps after idle, so the first request after a pause can be a few seconds slower (cold start).
- The `*.ntempurl.com` URL is temporary - keep it in the README / submission notes and inject it into the frontend's `environment.prod.ts` at build time rather than hard-coding it in source.

### 8.7 Live URLs

- API base: https://atbest2020-001-site1.ntempurl.com
- Swagger: https://atbest2020-001-site1.ntempurl.com/swagger/index.html

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

### 9.6 Database Transactions

Multi-step workflow operations that change a request's status **and** append a `WorkflowHistory` audit row are wrapped in a single database transaction so the two writes can never be persisted half-way - either both commit or both roll back.

- The boundary is owned by the Application layer through `IUnitOfWork.ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, ...)`. Repositories never open transactions or call `SaveChanges` themselves; the service decides what is atomic.
- The implementation (`UnitOfWork.ExecuteInTransactionAsync`) opens the transaction at `IsolationLevel.Serializable`, calls `operation`, commits on success, and rolls back on any exception. Serializable isolation also prevents two approvers from acting on the same pending request concurrently.
- The transaction runs through the provider's execution strategy (`Database.CreateExecutionStrategy().ExecuteAsync(...)`) so it cooperates with `EnableRetryOnFailure` - a transient SQL failure retries the whole load-mutate-save block atomically rather than re-running a partial write.
- Used by: `SponsorshipRequestService.SubmitAsync` / `CancelAsync` and `WorkflowService.ManagerDecisionAsync` / `FinanceDecisionAsync`. (Single-row writes such as create/update do not need an explicit transaction - one `SaveChanges` is already atomic.)

`EnableRetryOnFailure` is configured in `Infrastructure/DependencyInjection.cs` (max 3 retries, up to 5s delay).

### 9.7 CORS

Origins are read from `Cors:AllowedOrigins` in configuration. The Angular dev URL `http://localhost:4200` is allowed in Development; production hosts and the deployed SmarterASP.NET URL must be added to the list and the API restarted. The policy is named `AllowFrontend`, restricts headers to `Content-Type` and `Authorization`, allows the four HTTP verbs in use, and enables credentials so the refresh cookie can flow.

### 9.8 Compression

Brotli and Gzip response compression is enabled for JSON, including over HTTPS, at the Fastest level - good default trade-off for an API.

### 9.9 Cookie Policy

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
- IUnitOfWork wraps EF Core's SaveChanges and exposes `ExecuteInTransactionAsync` for multi-write atomic operations. Repositories never call SaveChanges or open transactions directly - that responsibility belongs to the service so transactional boundaries are explicit (see section 9.6). Workflow status changes and their audit-history rows commit inside one serializable transaction; single-row writes rely on SaveChanges being atomic on its own.
- File upload (SupportingDocumentPath) is in the schema but the upload endpoint is intentionally not implemented - the column is reserved for a future iteration.
- In-memory cache (IMemoryCache) is fine for single-instance deployments. A multi-node deployment would swap `MemoryCacheService` for a distributed cache (Redis) by re-binding `ICacheService`.
- Swagger is left enabled in all environments so the assessor can drive the API directly. In a real production deployment it would be Development-only.

---

## 12. Testing

A dedicated test project `src/Sponsorship.Application.Tests` (added to `Sponsorship.sln` alongside the other projects) covers all four layers from a single assembly. It references `Sponsorship.Infrastructure`, which transitively pulls in Application and Domain, so Domain, Application, and Infrastructure code can be exercised without multiple test projects. **166 tests, all passing.**

### 12.1 Test Stack

| Concern           | Tool                                                                              |
| ----------------- | --------------------------------------------------------------------------------- |
| Test framework    | xUnit                                                                             |
| Mocking           | NSubstitute (substitutes for every Application port)                              |
| Assertions        | FluentAssertions 6.12.x (Apache-2.0; 7.x/8.x require a commercial license)         |
| Mock database     | EF Core InMemory provider (isolated database per test)                            |
| Coverage          | coverlet.collector                                                                |

Shared helpers live in `TestSupport/`:

- `FixedDateTimeProvider` - deterministic `IDateTimeProvider` so tests never depend on the wall clock.
- `PassThroughCacheService` - a real `ICacheService` double that always invokes the factory and records `Remove` / `RemoveByPrefix` calls, so cache-eviction behaviour is assertable.
- `TestData` - factory for building domain entities (User, Role, SponsorshipType, SponsorshipRequest, RefreshToken) with sensible defaults.
- `InMemoryDb` - spins up an isolated `AppDbContext` per call so repository tests never share state.
- `UnitOfWorkSubstitute` - an `IUnitOfWork` mock whose `ExecuteInTransactionAsync` runs the supplied operation in-line (no real database in unit tests), so transaction-wrapped service code is asserted exactly like the non-transactional path - the mutation runs and any `SaveChangesAsync` or exception propagates as it would inside the real serializable transaction.

### 12.2 Running the Tests

```powershell
# From Backend/ - run the whole suite
dotnet test

# Or just the test project
dotnet test src/Sponsorship.Application.Tests

# With code coverage
dotnet test --collect:"XPlat Code Coverage"
```

### 12.3 Coverage by Area

#### Domain - workflow state machine (`SponsorshipRequestTests`)

- Constructor starts a request in `Draft` with all fields set and empty history.
- `UpdateDraft` edits fields and stamps `UpdatedAt` while `Draft`; throws `DomainException` once the request has left `Draft`.
- `Submit`: `Draft -> PendingManagerApproval` and records a history row; throws `InvalidWorkflowTransitionException` from every non-Draft state.
- `Cancel`: allowed from `Draft`, `PendingManagerApproval`, and `PendingFinanceReview`; throws from the terminal states (`Approved`, `Rejected`, `Cancelled`).
- `ManagerApprove -> PendingFinanceReview`, `ManagerReject -> Rejected`; both throw from any wrong state.
- `FinanceApprove -> Approved`, `FinanceReject -> Rejected`; both throw from any wrong state.
- Full approval path (`Submit -> ManagerApprove -> FinanceApprove`) accumulates three ordered history entries.

#### Domain - refresh token lifecycle (`RefreshTokenTests`)

- A new token is active and not revoked.
- `IsExpired` boundary check (`now >= ExpiresAt`).
- `Revoke` marks the token revoked and inactive; the overload records the successor (`ReplacedByToken`).
- An expired token is inactive even when not revoked.

#### Application - AuthService (`AuthServiceTests`)

- **Login**: valid credentials issue tokens and persist the refresh token; unknown email -> 401 (password hasher never called); inactive user -> 401; wrong password -> 401 (nothing saved).
- **Refresh**: unknown token -> 401; **already-revoked token triggers full chain revocation -> 401 (reuse/theft detection)**; expired token -> 401; missing user -> 401; inactive user -> 401; happy path rotates the token (old row revoked with `ReplacedByToken` set, new row added, new pair returned).
- **Logout**: unknown token is a no-op (no save); an active token is revoked and saved; an already-revoked token keeps its original timestamp but still saves.

#### Application - SponsorshipRequestService (`SponsorshipRequestServiceTests`)

- **Create**: unauthenticated -> 401; missing user -> 404; missing type -> 404; inactive type -> `DomainException`; happy path persists a request owned by the current user (RequestorId taken from auth context, never the DTO).
- **Update**: missing -> 404; non-owner -> `ForbiddenException`; inactive type -> `DomainException`; non-draft request -> `DomainException`; happy path updates and saves.
- **GetById**: missing -> 404; requestor reading someone else's request -> `ForbiddenException`; owner can read own; Manager / FinanceAdmin / SystemAdmin can read any.
- **List**: a Requestor sees only their own rows; Manager / FinanceAdmin / SystemAdmin see all.
- **Submit / Cancel**: non-owner -> `ForbiddenException`; owner transitions and saves (cancel records remarks).

#### Application - WorkflowService (`WorkflowServiceTests`)

- Manager and Finance queues return only the requests in the matching status.
- **ManagerDecision**: no user -> 401; missing -> 404; approve advances to `PendingFinanceReview` and evicts the history cache; reject moves to `Rejected`.
- **FinanceDecision**: missing -> 404; approve moves to `Approved`; reject moves to `Rejected`.
- **GetHistory**: missing -> 404; a Requestor cannot read another user's history (`ForbiddenException`); the owner gets ordered history served through the cache layer; Manager / FinanceAdmin / SystemAdmin can read any history.

#### Application - Validators (FluentValidation.TestHelper)

- `LoginDtoValidator` - valid passes; empty / malformed / over-256-char email and empty / over-200-char password fail.
- `CreateRequestDtoValidator` - valid passes; event date today is allowed, past dates fail; empty or over-length Title / Department / EventName / Purpose fail; non-positive `SponsorshipTypeId`; non-positive amount; amount with more than two decimals; over-length ExpectedBenefit / Remarks; null optional fields pass.
- `UpdateRequestDtoValidator` - mirrors the create rules (valid, past date, empty title, non-positive amount, three-decimal amount).
- `ApprovalActionDtoValidator` - valid Approve / Reject pass; null remarks allowed; out-of-range enum and remarks over 1000 chars fail.
- `CreateSponsorshipTypeDtoValidator` / `UpdateSponsorshipTypeDtoValidator` - valid pass; empty name and over-100-char name fail.

#### Application - Mappers (`SponsorshipRequestMapperTests`)

- Request -> DTO maps all fields plus the requestor and type navigations, and tolerates missing navigations by emitting empty strings; WorkflowHistory -> DTO and SponsorshipType -> DTO mappings.

#### Infrastructure - PasswordHasher (`PasswordHasherTests`)

- Hash never returns the plaintext; `Verify` returns true for the correct password and false for the wrong one; hashing the same password twice yields different salted hashes that both verify.

#### Infrastructure - JwtTokenService (`JwtTokenServiceTests`)

- Access-token expiry is derived from settings; the token embeds `sub`, email, role, name, issuer, and audience claims; refresh tokens are unique and decode to 64 random bytes; `GetPrincipalFromExpiredToken` returns a principal for a validly-signed (even expired) token but rejects garbage, a token signed with a different key, and a token with the wrong issuer.

#### Infrastructure - Repositories against the in-memory database

- `SponsorshipRequestRepository` - `GetById` eager-loads requestor and type, and returns null when absent; `ListByStatus` filters; `ListByRequestor` scopes to one user; `ListAll` orders by `CreatedAt` descending; `GetByIdWithHistory` includes the workflow history.
- `RefreshTokenRepository` - add then get-by-token round-trips; null for unknown tokens; `RevokeAllForUser` revokes only that user's active tokens, leaving already-revoked timestamps and other users' tokens untouched.
- `UserRepository` - `GetByEmail` / `GetById` load the Role navigation; null for unknown email.
- `SponsorshipTypeRepository` - `ListAsync(activeOnly: true)` excludes disabled types; `ListAsync(activeOnly: false)` returns everything ordered by name; `GetById` returns a match or null; `Add` persists a new active type.

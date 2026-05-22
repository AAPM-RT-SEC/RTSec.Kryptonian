# User Management & Authentication Plan
## RTSec.Kryptonian Gateway

## Context

The Kryptonian gateway currently authenticates every request with a single shared `X-API-Key` header value set at deploy time via environment variable. Anyone who has the URL and key can read or modify all settings. There is no user identity, no roles, and no login screen. This plan adds a proper multi-user authentication layer, role-based access control (RBAC), a browser login screen, first-run bootstrap, and an API key generation feature — all designed to work identically in Docker on-prem and in Azure Container Apps.

---

## Architecture Overview

| Concern | Decision |
|---|---|
| Session auth (UI) | JWT Bearer tokens (8 h lifetime, no refresh, re-login on expiry) |
| Programmatic auth (REST) | User-generated API keys stored as SHA-256 hash in DB |
| Legacy static keys | `KRYPTONIAN__ADMINAPI__APIKEYS` env var continues to work as System Admin for backward compat |
| Password hashing | BCrypt (BCrypt.Net-Next NuGet, cost factor 12) |
| JWT signing key | `KRYPTONIAN__AUTH__JWTSECRET` env var (≥32 chars); dev fallback constant with warning |
| DB tables | New `users` + `api_keys` tables in existing EF Core context |
| Bootstrap | UI wizard **and** env-var seeding (both supported) |
| Roles | `Standard` · `DeviceAdmin` · `SystemAdmin` |

---

## Roles & Permission Matrix

| Capability | Standard | DeviceAdmin | SystemAdmin |
|---|:---:|:---:|:---:|
| View dashboard / events | ✓ | ✓ | ✓ |
| View devices & certificates | ✓ | ✓ | ✓ |
| View EST profiles & CA backends | ✓ | ✓ | ✓ |
| Register / approve / remove devices | | ✓ | ✓ |
| Create / edit / delete CA backends | | | ✓ |
| Activate CA backend | | | ✓ |
| Create / edit EST profiles | | | ✓ |
| Modify gateway & notification settings | | | ✓ |
| Generate / revoke own API keys | | ✓ | ✓ |
| Create / manage users | | | ✓ |
| View all users' API keys | | | ✓ |

---

## Phase 1 — Domain & Infrastructure (new entities)

### 1.1 New Domain Entities

**`src/RTSec.Kryptonian.Domain/Entities/User.cs`**
- Id, Username (unique), Email (unique), PasswordHash (BCrypt), Role (enum), IsActive, ApiKeys collection

**`src/RTSec.Kryptonian.Domain/Entities/ApiKey.cs`**
- Id, Name, KeyHash (SHA-256, unique), Prefix (first 8 chars for display), UserId (FK), Role (copied from user), ExpiresAt, RevokedAt, LastUsedAt

**`src/RTSec.Kryptonian.Domain/Enums/UserRole.cs`**
- Standard, DeviceAdmin, SystemAdmin

### 1.2 EF Core Configuration

Add `UserConfiguration` and `ApiKeyConfiguration` in Infrastructure Configurations folder (snake_case tables `users`, `api_keys`). Add DbSets to `KryptonianDbContext`. Uses existing `EnsureCreated()` — works for fresh Docker installs. Existing PostgreSQL installs will need manual table creation (see SQL at bottom).

### 1.3 Repositories

- `IUserRepository` — `FindByUsernameAsync`, `FindByIdAsync`, `AnyAsync`
- `IApiKeyRepository` — `GetByHashAsync`, `GetByUserIdAsync`, `GetAllActiveAsync`
- Both added to `IUnitOfWork`

---

## Phase 2 — Application Layer (services)

### 2.1 `AuthService` (`src/RTSec.Kryptonian.Application/Services/AuthService.cs`)

- `IsSetupRequiredAsync()` → true when Users table empty
- `BootstrapAsync(dto)` → creates first SystemAdmin; throws if users exist
- `LoginAsync(dto)` → BCrypt verify, returns JWT
- JWT Claims: `sub` (userId), `name` (username), `role` (Standard|DeviceAdmin|SystemAdmin), `jti`, 8h expiry

### 2.2 `UserService` (`src/RTSec.Kryptonian.Application/Services/UserService.cs`)

- `ListUsersAsync()`, `CreateUserAsync()`, `UpdateUserAsync()`, `DeleteUserAsync()` (blocks deleting last SystemAdmin)
- `GenerateApiKeyAsync(userId, name, expiresAt?)` → raw key format `kry_<base64url(32 bytes)>`, stores SHA-256 hash, returns raw key once
- `RevokeApiKeyAsync(keyId, requestingUserId)` → SystemAdmin can revoke any, others only own

---

## Phase 3 — API Controllers

### 3.1 `AuthController` — `/api/auth`

| Route | Auth | Purpose |
|---|---|---|
| `GET /api/auth/status` | None | `{ mode: "setup"|"login"|"ok", username?, role? }` |
| `POST /api/auth/setup` | None | Creates first admin; 409 if users exist |
| `POST /api/auth/login` | None | `{ username, password }` → JWT response |
| `POST /api/auth/logout` | JWT | Client-side; returns 204 |
| `GET /api/auth/me` | JWT | Current user profile |

### 3.2 `UsersController` — `/api/users` (SystemAdmin only)

GET list, POST create, GET by id, PUT update (role/password/active), DELETE

### 3.3 `ApiKeysController` — `/api/users/{id}/api-keys` 

GET list (own or SystemAdmin), POST generate (DeviceAdmin+), DELETE revoke (own or SystemAdmin)

**Raw key format:** `kry_<base64url(32 random bytes)>`

---

## Phase 4 — Authentication Middleware

### 4.1 Dual-Scheme Auth

```csharp
AddAuthentication("PolicyScheme")
  .AddPolicyScheme("PolicyScheme", ..., opts =>
      opts.ForwardDefaultSelector = ctx =>
          ctx.Request.Headers.ContainsKey("Authorization") ? "Jwt" : "ApiKey")
  .AddJwtBearer("Jwt", ...)
  .AddScheme<..., ApiKeyAuthenticationHandler>("ApiKey", ...)
```

### 4.2 Authorization Policies

- `StandardOrAbove` — any authenticated user
- `DeviceAdmin` — DeviceAdmin or SystemAdmin role
- `SystemAdmin` — SystemAdmin role only

### 4.3 Updated ApiKeyAuthenticationHandler

1. Config-based static keys → SystemAdmin role (backward compat)
2. DB-stored `kry_*` keys → role from DB record
3. Dev bypass → StandardOrAbove when no keys configured and dev mode
4. `[AllowAnonymous]` on Setup + Login endpoints

### 4.4 Controller Role Updates

| Controller | GET | Mutations |
|---|---|---|
| Devices | StandardOrAbove | DeviceAdmin |
| CA Backends | StandardOrAbove | SystemAdmin |
| EST Profiles | StandardOrAbove | SystemAdmin |
| Gateway Settings | StandardOrAbove | SystemAdmin |
| Notification Settings | StandardOrAbove | SystemAdmin |

---

## Phase 5 — Frontend: Auth State & Bootstrap/Login Views

### 5.1 `api.ts` Changes

- Replace static `X-API-Key` header with JWT Bearer from `localStorage`
- Add: `getAuthStatus()`, `setup()`, `login()`, `getApiKeys()`, `generateApiKey()`, `revokeApiKey()`, `getUsers()`, `createUser()`, `updateUser()`, `deleteUser()`
- 401 response handler: clear token, trigger re-login

### 5.2 Auth Bootstrap Flow in `App.tsx`

```
mount → GET /api/auth/status
  'setup' → <SetupPage>
  'login' → <LoginPage>
  'ok'    → load data → show shell with role-appropriate nav
```

Add `currentUser: { id, username, role } | null` state.

### 5.3 Pages Added

- `SetupPage.tsx` — first-admin creation form
- `LoginPage.tsx` — username/password form (uses existing dark sidebar as background)
- `UsersPage.tsx` — user table + role badges, add/edit modals (SystemAdmin only)
- `ApiKeysPage.tsx` — key table, generate modal with copy-once UX (DeviceAdmin+)

---

## Phase 6 — Frontend: Role-Based Navigation

| Nav Item | Standard | DeviceAdmin | SystemAdmin |
|---|:---:|:---:|:---:|
| Dashboard | ✓ | ✓ | ✓ |
| Devices | ✓ | ✓ | ✓ |
| CA Backends | ✓ | ✓ | ✓ |
| EST Profiles | ✓ | ✓ | ✓ |
| Events | ✓ | ✓ | ✓ |
| Settings | | | ✓ |
| Users | | | ✓ |
| API Keys | | ✓ | ✓ |

In-page action buttons (Register Device, Create Backend, etc.) conditionally rendered by role. Sidebar footer shows logged-in username + "Sign out" button.

---

## Phase 7 — Docker & Configuration Updates

### `docker-compose.yml` additions

```yaml
KRYPTONIAN__AUTH__JWTSECRET: "${KRYPTONIAN_JWT_SECRET}"
# Optional seed:
# KRYPTONIAN__AUTH__INITIAL_ADMIN_USERNAME: admin
# KRYPTONIAN__AUTH__INITIAL_ADMIN_PASSWORD: changeme
```

### `appsettings.json` additions

```json
"Kryptonian": {
  "Auth": {
    "JwtSecret": "",
    "JwtLifetimeHours": 8,
    "InitialAdminUsername": "",
    "InitialAdminPassword": ""
  }
}
```

### Startup seed logic (Program.cs)

After `EnsureCreated()`, if no users exist AND env vars are set → auto-create first SystemAdmin.

---

## Bootstrap Flow (Complete Sequence)

```
Docker container starts
  └─ EnsureCreated() creates schema incl. users + api_keys tables
  └─ IsSetupRequired() check
      ├─ Env vars set → seed SystemAdmin → log confirmation
      └─ No env vars → skip (UI wizard handles it)

Browser opens gateway URL
  └─ GET /api/auth/status
      ├─ 'setup' → <SetupPage>
      ├─ 'login' → <LoginPage>
      └─ 'ok'    → normal shell
```

---

## NuGet Packages Added

| Package | Project |
|---|---|
| `BCrypt.Net-Next` 4.0.3 | Application |
| `System.IdentityModel.Tokens.Jwt` 7.7.0 | Application |
| `Microsoft.AspNetCore.Authentication.JwtBearer` 8.0.11 | Api |

---

## Manual Migration SQL (for existing PostgreSQL installs)

```sql
CREATE TABLE IF NOT EXISTS users (
    id UUID PRIMARY KEY,
    username VARCHAR(100) NOT NULL UNIQUE,
    email VARCHAR(256) NOT NULL UNIQUE,
    password_hash TEXT NOT NULL,
    role VARCHAR(50) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP NOT NULL
);

CREATE TABLE IF NOT EXISTS api_keys (
    id UUID PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    key_hash VARCHAR(64) NOT NULL UNIQUE,
    prefix VARCHAR(20) NOT NULL,
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    role VARCHAR(50) NOT NULL,
    expires_at TIMESTAMPTZ,
    revoked_at TIMESTAMPTZ,
    last_used_at TIMESTAMPTZ,
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_api_keys_user_id ON api_keys(user_id);
```

---

## Verification Checklist

1. Fresh Docker (no env vars) → Setup page appears → create admin → dashboard with SystemAdmin role
2. Fresh Docker (env vars set) → Login page → login with seeded credentials → dashboard
3. Standard user can view but not edit devices, cannot see Settings/Users/API Keys nav
4. DeviceAdmin can register devices, sees API Keys nav, cannot see Settings
5. SystemAdmin has full access
6. `curl -H "X-API-Key: kry_<generated>" /api/devices` → 200 OK
7. Revoke key → same curl → 401
8. Legacy `KRYPTONIAN__ADMINAPI__APIKEYS` key still returns 200
9. SQLite on-prem Docker: all above tests pass

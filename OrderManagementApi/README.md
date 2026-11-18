# OrderManagement-SADC

## API authentication configuration

The API requires JWT configuration before startup. Configure `Jwt:Issuer`, `Jwt:Audience`, and `Jwt:SigningKey` through a trusted configuration provider (for example environment variables `Jwt__Issuer`, `Jwt__Audience`, and `Jwt__SigningKey`, or a secret store). The signing key must be at least 32 UTF-8 bytes. No development fallback key is provided. After JWT validation, the token's `sub` must match an application user's Identity ID; the API loads that user's roles from the database and uses them for authorization. Assign `Orders.Read`, `Orders.Write`, or `Orders.Admin` roles as required by the route.

### Register and log in

Create an account with `POST /api/auth/register` using `firstName`, `lastName`, `email`, and `password`. Passwords must contain at least 12 characters, including uppercase and lowercase letters, a number, and a non-alphanumeric character. Registration creates an account but does not issue a token or grant order permissions.

Log in with `POST /api/auth/login` using `email` and `password`. The response contains an `accessToken` and its UTC expiration, and sets a rotating refresh token in an HttpOnly, Secure, SameSite=Strict cookie. Send the access token on protected API requests as `Authorization: Bearer <accessToken>`. Access tokens are valid for 60 minutes by default; configure `Jwt:AccessTokenMinutes` from 5 to 1440 minutes to change the lifetime. The refresh session lasts 14 days by default; configure `Jwt:RefreshTokenDays` from 1 to 90 days.

The SPA calls `POST /api/auth/refresh` during startup to restore a session after a page reload. Refresh tokens are stored hashed in the Identity user-token table and rotated on every refresh. `POST /api/auth/logout` revokes the active refresh token and clears the cookie. The access token remains in frontend memory only.

New accounts have no order roles. An administrator must assign the appropriate Identity role before the account can use protected routes; successful login alone authenticates the user but does not grant authorization.

### Roles and sample data

The Development environment enables idempotent startup seeding after the EF Core migrations have been applied (`dotnet ef database update`). It creates the `Admin`, `Orders.Read`, `Orders.Write`, and `Orders.Admin` roles, assigns `Admin` to the existing `ctshabalala89@gmail.com` account, and seeds 1,024 demo orders and 16 customers across all 16 SADC countries using currencies permitted by the SADC currency policy. Zimbabwe sample orders include both ZWL and USD. Set `SeedData:Enabled` to `false` to disable seeding, or configure `SeedData:AdminEmail` and `SeedData:SeedOrders` as required. Production seeding is disabled unless explicitly enabled.

Role management is restricted to users with the `Admin` or `Orders.Admin` role:

- `GET /api/roles` lists seeded roles.
- `GET /api/roles/users/{userId}` returns a user's assigned roles (`GetRolesAsync`).
- `POST /api/roles/users/{userId}` assigns one or more existing roles; assignments are additive.

Register the designated admin account before starting the API with seeding enabled. If it does not exist, startup logs a warning and continues; register it and restart with seeding enabled to assign the `Admin` role.

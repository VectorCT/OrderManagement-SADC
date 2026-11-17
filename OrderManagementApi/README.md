# OrderManagement-SADC

## API authentication configuration

The API requires JWT configuration before startup. Configure `Jwt:Issuer`, `Jwt:Audience`, and `Jwt:SigningKey` through a trusted configuration provider (for example environment variables `Jwt__Issuer`, `Jwt__Audience`, and `Jwt__SigningKey`, or a secret store). The signing key must be at least 32 UTF-8 bytes. No development fallback key is provided. After JWT validation, the token's `sub` must match an application user's Identity ID; the API loads that user's roles from the database and uses them for authorization. Assign `Orders.Read`, `Orders.Write`, or `Orders.Admin` roles as required by the route.

### Register and log in

Create an account with `POST /api/auth/register` using `firstName`, `lastName`, `email`, and `password`. Passwords must contain at least 12 characters, including uppercase and lowercase letters, a number, and a non-alphanumeric character. Registration creates an account but does not issue a token or grant order permissions.

Log in with `POST /api/auth/login` using `email` and `password`. The response contains an `accessToken` and its UTC expiration. Send the token on protected API requests as `Authorization: Bearer <accessToken>`. Access tokens are valid for 60 minutes by default; configure `Jwt:AccessTokenMinutes` from 5 to 1440 minutes to change the lifetime.

New accounts have no order roles. An administrator must assign the appropriate Identity role before the account can use protected routes; successful login alone authenticates the user but does not grant authorization.

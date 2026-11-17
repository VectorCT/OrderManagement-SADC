# OrderManagement-SADC

## API authentication configuration

The API requires JWT configuration before startup. Configure `Jwt:Issuer`, `Jwt:Audience`, and `Jwt:SigningKey` through a trusted configuration provider (for example environment variables `Jwt__Issuer`, `Jwt__Audience`, and `Jwt__SigningKey`, or a secret store). The signing key must be at least 32 UTF-8 bytes. No development fallback key is provided. After JWT validation, the token's `sub` must match an application user's Identity ID; the API loads that user's roles from the database and uses them for authorization. Assign `Orders.Read`, `Orders.Write`, or `Orders.Admin` roles as required by the route.
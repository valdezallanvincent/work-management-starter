# 0008. Use Identity's password hasher without the full Identity store

Status: accepted

## Context

The application needs secure password storage, login with lockout, a single role per user, and
administrator-provisioned accounts. Users are also a domain concept: projects have owners, and
work items have reporters and assignees.

## Decision

Model `User` as a domain entity. Hash passwords with ASP.NET Core Identity's
`PasswordHasher<TUser>` behind an Application interface, and implement lockout and role
assignment in the domain. Do not use `UserManager`, `SignInManager` or the Identity table schema.

## Alternatives considered

- **Full ASP.NET Core Identity.** Provides email confirmation, password reset tokens, two-factor
  authentication, external logins, multiple roles and claims. It also brings its own user type
  and seven tables, and pulls the domain's `User` toward an infrastructure base class (or forces
  two user records kept in sync). Most of those features are unused by this application.
- **An external identity provider** (Microsoft Entra ID, Auth0, Keycloak). The strongest option
  for production: MFA, conditional access and single sign-on without owning credentials. It
  needs a tenant and configuration that a self-contained starter cannot assume. The API's JWT
  validation would remain; only issuing would move out.

## Consequences

- No custom cryptography: hashing, salting, iteration counts and the hash format upgrade path come
  from the framework, and `SuccessRehashNeeded` is honoured on login.
- The user remains a plain domain entity with its own rules and tests.
- Features such as password reset by email and two-factor authentication are not available and
  would need to be built, or be a reason to move to full Identity or an external provider.

## When to revisit

As soon as self-service password reset, MFA or single sign-on is required. For an internal
business tool in an organisation with Entra ID, an external provider is the better choice.

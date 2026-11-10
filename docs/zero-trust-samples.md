# Zero Trust demo additions

These are teaching samples, not a claim of production readiness. Zero Trust means
**verify explicitly**, **use least privilege**, and **assume breach**. A small image,
an unsigned numeric type, or a private network is useful defense in depth, not a
replacement for authorization.

## Repository observations

The existing solution uses .NET 10, chiseled/non-root container examples, JWT bearer
authentication, delegated Entra scopes, account ownership checks, a focused Azure
credential helper, OpenTelemetry, dependency lock files
and a dependency audit workflow. Terraform models workload identities, data-plane
roles, private endpoints, TLS and an optional outbound firewall.

Some are intentionally incomplete demo switches: PaymentsApi had no fallback
policy, used DefaultAzureCredential, and kept its container hardening commented
out. Both APIs enabled IdentityModel PII/token logging in Development. Storage
factories accepted connection strings outside Development. Private endpoints exist
but storage public access is enabled, and the firewall module is not wired into the
development environment. Do not equate the scaffold with a deployed security boundary.

## 1. Deny by default, then grant an exact delegated permission

`src/MiniBank.ServiceDefaults/AuthorizationExtensions.cs` creates a shared policy
for each API. Both the default policy (bare `RequireAuthorization`) and fallback
policy (forgotten endpoint metadata) require authentication **and** the API's exact,
case-sensitive delegated scope. A `roles` claim does not substitute for `scp`.
Existing resource ownership checks remain necessary: scope permission alone does
not authorize debiting somebody else's account.

In PaymentsApi, `/test-endpoint` deliberately has no authorization metadata so the
fallback can be demonstrated. Without a token it returns 401; with a valid token
but the wrong scope, 403; with `Payment.Create`, 200. This changes the previous
intentionally insecure endpoint to a secure-by-default example. Health probes are
explicit anonymous exceptions; OpenAPI/Swagger remain Development-only.

The integration tests use locally signed JWTs and an offline issuer configuration
inside the test host only, retaining the actual bearer signature, audience,
lifetime and scope checks. No signing key or bypass is installed in the app.

```powershell
dotnet test src\PaymentsApiTests\PaymentsApiTests.csproj
dotnet test src\ProcessingTests\ProcessingTests.csproj
```

## 2. Treat a well-typed payment as untrusted input

`uint` rejects negative JSON account IDs, but still admits zero and values the
signed-32-bit ledger cannot represent. `PaymentValidator` now checks that full
range, allows only EUR/USD/GBP, and accepts positive amounts up to 1,000,000 with
at most two fractional digits. These are explicit **demo business limits**, not
universal banking rules; the existing 13,000 high-amount example remains valid.
Descriptions remain required and limited to 30 characters and now reject control
characters. JSON fields outside the contract are rejected rather than ignored.

The validation decorator is now outermost: rejected input cannot query accounts
or publish a payment. Tests verify both boundaries and absence of those side
effects. Try `amount: 0.001`, `fromAccountId: 0`, `currency: "../"`, or an additional
`userId` field. Do not silently clamp or round an invalid transfer into a valid one.
This sample covers HTTP input, **not** worker authorization, queue provenance or
post-conversion currency/amount validation; those remain separate trust boundaries.

## 3. Bound work before trusting the caller

Both APIs use `AddApiRequestBudgets`: 4,096-byte Kestrel request bodies, at most
32 headers/16 KiB total, and a 10-second header timeout. A per-process global
fixed-window limiter admits 60 requests/minute with **no queue** and returns
429 Problem Details with `Retry-After` when exhausted. It runs before
authentication, so anonymous traffic also consumes the budget instead of
performing unbounded token validation. Anonymous health probes are explicitly
excluded, preventing overload from manufacturing failed liveness probes.

The limits are deliberately small for a live demo, apply to all API routes, and
are fixed constants in `ApiRequestBudgetExtensions`. One global partition also
avoids allocating a limiter per arbitrary forwarded IP, username or claim.
Integration tests admit exactly 60 requests then reject the 61st; real Kestrel
tests accept 4,096 bytes and reject 4,097 bytes, with and without Content-Length.
TestServer alone would not prove the Kestrel body limit.

This is not DDoS protection or a distributed quota. Fixed windows allow boundary
bursts, each replica has its own budget, and one caller can exhaust the shared
budget. Add measured edge/WAF protection and bounded per-principal quotas as a
production exercise. Configure the same limits at the reverse proxy; do not trust
arbitrary forwarded headers to identify callers.

## Research and further recommendations

Primary references consulted:

- [Microsoft: Develop using Zero Trust principles](https://learn.microsoft.com/security/zero-trust/develop/overview)
- [ASP.NET Core: JWT bearer authentication](https://learn.microsoft.com/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0)
- [ASP.NET Core: Rate limiting](https://learn.microsoft.com/aspnet/core/performance/rate-limit?view=aspnetcore-10.0)
- [OWASP: Input validation](https://cheatsheetseries.owasp.org/cheatsheets/Input_Validation_Cheat_Sheet.html)
- [System.Text.Json: Disallow unmapped members](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/missing-members)

Useful next demonstrations, intentionally not implied by the samples above:

- Migrate ownership from mutable `preferred_username` to a tenant/object identity
  (`tid` + `oid`). This needs a deliberate data and emulator-seed migration; never
  silently reinterpret existing username rows.
- Revalidate queue envelopes and authorization at the worker trust boundary; add a
  quarantine path for malformed messages and bound retries. A queue producer's
  identity is not proof of the end user's authority.
- Add durable idempotency and an atomic ledger/outbox. Storage Queue delivery is
  at least once, while the current worker generates fresh transaction IDs and
  writes debit/credit rows individually; retries can duplicate or partially apply
  payments. A cache or in-memory idempotency dictionary would not fix this.
- Demonstrate Conditional Access/authentication context for high-value transfers
  using a real tenant and claims challenges. Logging a large amount is detection,
  not step-up authentication or fraud prevention.
- Pin deployed images by digest, generate/verify SBOMs and attestations, and enforce
  image/dependency scanning in CI. Keep reproducible, tested image updates.
- Deploy storage with public access disabled and run Terraform from a private,
  Entra-authorized runner. Private endpoints alone do not disable public endpoints.
  Wire an affordable, tested egress boundary rather than claiming the optional,
  currently commented-out firewall is active.

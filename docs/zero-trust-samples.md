# Zero Trust demo additions

These are teaching samples, not a claim of production readiness. Zero Trust means
**verify explicitly**, **use least privilege**, and **assume breach**. A small image,
an unsigned numeric type, or a private network is useful defense in depth, not a
replacement for authorization.

For additional, not-yet-implemented recommendations, see
[Modern Zero Trust practices](zero-trust-modern-practices.md). For guidance beyond
the API scope, see [Zero Trust for agentic AI](zero-trust-agentic-ai.md).

## Repository observations

The solution now targets .NET 11, with chiseled/non-root container examples, JWT bearer
authentication, delegated Entra scopes, account ownership checks, a focused Azure
credential helper, OpenTelemetry, dependency lock files
and a dependency audit workflow. Terraform models workload identities, data-plane
roles, private endpoints, TLS and an optional outbound firewall.

Aspire orchestrates the local workloads and Azurite. Deployment images are built
with .NET SDK container publishing rather than Dockerfiles; see the
[container publishing commands](../README.md#container-images). The optional
Dependency-Track demo has its own
[named Compose file](../README.md#optional-dependency-track-demo).

The code targets the .NET 11.0.0 release and uses the .NET 11 ASP.NET Core
authentication, hosting, and OpenAPI packages. .NET 11 is a Standard Term Support
(STS) release; check the [.NET support policy](https://dotnet.microsoft.com/platform/support/policy/dotnet-core)
and keep the SDK, container runtime, and servicing packages current. Upgrade and
dependency lock files are part of the sample so the deployment images and local
builds use the same major runtime.

PaymentsApi's container hardening remains an intentionally commented demo switch.
The authorization fallback, focused Azure credential selection, safe logging
defaults and Development-only connection strings described below are implemented.
Private endpoints exist but storage public access is enabled, and the firewall
module is not wired into the development environment. Do not equate the scaffold
with a deployed security boundary.

## 1. Deny by default, then grant an exact delegated permission

The account endpoints declare `.RequireAuthorization()` and
`.RequireScope(requiredScope)` using Microsoft.Identity.Web. The endpoint
`RequireScope` method adds metadata and must be paired with `RequireAuthorization`
to enforce it.
`src/MiniBank.ServiceDefaults/AuthorizationExtensions.cs` registers the scope
handler with `AddRequiredScopeAuthorization` and sets the same scope requirement
on the default and fallback policies. Named policies must declare their own
scope requirements. Scope matching is
exact and case-sensitive. The handler checks both raw `scp` and mapped
`http://schemas.microsoft.com/identity/claims/scope` claims and handles
space-delimited scope values. Unlike the previous custom assertion, it does not
accept a literal `scope` claim; Entra tokens use `scp`. A `roles` claim does not
substitute for a delegated scope. Existing resource ownership checks remain
necessary: scope permission alone does not authorize debiting somebody else's
account.

In PaymentsApi, `/test-endpoint` deliberately has no authorization metadata so the
fallback can be demonstrated. Without a token it returns 401; with a valid token
but without `Payment.Read`, 403; with `Payment.Read`, 200. It still returns a fixed
demo message. `GET /accounts/{accountId}/payments` uses the default `Payment.Read`
policy and returns processed debit and credit entries from the owned account's
partition, not queued submissions. Missing and non-owned accounts return 404.
`POST /payment` requires `Payment.Write` instead of `Payment.Read` and still checks
that the caller owns the source account. Health probes are explicit anonymous
exceptions; OpenAPI/Swagger remain Development-only.

Account ownership uses the authenticated Entra `tid` and `oid` claims, not the
mutable `preferred_username` display/login value. Both claims must occur exactly
once and contain non-empty GUIDs; missing, malformed, or ambiguous identity fails
closed before account access. Both APIs disable inbound claim mapping so these
names remain unchanged in the `ClaimsPrincipal`. The scope handler also accepts
mapped scope claims, but a correctly scoped token cannot pass ownership checks
if `tid` and `oid` have been renamed by inbound mapping.
The Accounts API filters on both owner fields and
checks ownership again for individual reads. The Payments API carries the stable
owner in its queue event; account responses do not serialize owner identifiers.

This is an intentional storage and queue schema change, not an automatic data
migration. Existing table rows containing only `UserId` have no trusted tenant /
object mapping and are denied; map each account to verified `OwnerTenantId` and
`OwnerObjectId` values through a separately reviewed migration. Do not copy
`preferred_username` into either field or infer an object ID from it. Legacy
queue messages without an owner are rejected and deleted; drain or review
in-flight messages before switching producers to avoid dropping accepted work.
The local Aspire seed requires
`MiniBank:DemoOwnerTenantId` and `MiniBank:DemoOwnerObjectId` configuration with
the exact Entra identity used to authenticate; use non-secret app-host user
secrets or environment variables. These IDs are identifiers, not credentials.
Accounts 1 and 2 belong to that configured owner. The optional
`MiniBank:AdditionalDemoOwnerObjectId` seeds accounts 3 and 4 for a second user
in the same tenant, without changing accounts 1 and 2. Development configuration
sets this second owner to `b1929754-4fe7-4dad-b297-062167269d28`.
Each owner gets an Everyday account and a Savings account. The seed upserts these
fixed account IDs on AppHost startup and leaves other account rows untouched.

The integration tests use locally signed JWTs and an offline issuer configuration
inside the test host only, retaining the actual bearer signature, audience,
lifetime and scope checks. No signing key or bypass is installed in the app.

```powershell
dotnet test src\PaymentsApiTests\PaymentsApiTests.csproj
dotnet test src\ProcessingTests\ProcessingTests.csproj
```

## 2. Treat a well-typed payment as untrusted input

PaymentsApi uses `JsonNumberHandling.Strict`: numeric JSON fields must contain
numbers, not quoted numeric strings. OpenAPI therefore describes numeric inputs
with a single numeric type, including the payment-history `accountId` route
parameter, without custom schema transformations.

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
The API and worker share `PaymentContractValidator`; the worker rechecks the original message before
currency conversion and validates the converted payment again before ledger writes.

At execution, the worker requires a well-formed owner and payment, confirms the
source and destination accounts still exist, and checks that the current source
owner still matches the event's tenant/object IDs. This uses a table-scoped
read-only role. Rejected messages emit structured event **1003 /
PaymentMessageRejected** and are deleted to avoid endless poison-message retries;
transient storage/write exceptions are not converted into successful processing.
The producer remains a trusted queue boundary: a sender with queue write access
could forge the envelope. This does not provide transaction-bound approval,
idempotency, or atomic ledger updates.

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

## 4. Detect abuse without copying secrets into telemetry

Both APIs enable JWT bearer `IncludeErrorDetails` and IdentityModel `ShowPII`
only when `Environment.IsDevelopment()` is true. This exposes authentication
failure details in the `WWW-Authenticate` response header and allows PII in
IdentityModel diagnostics for local troubleshooting. Both settings are disabled
in all other environments. `LogCompleteSecurityArtifact` remains disabled, so
full security artifacts such as tokens are not enabled for logging. Development
logs may contain sensitive identity data; do not send them to shared dashboards.

AccountsApi sets `ShowPII` **after** `AddMicrosoftIdentityWebApi`, because that
registration resets the static flag. Setting the environment to Development
alone does not prevent that reset. Development configuration enables Debug logs
for `Microsoft.AspNetCore.Authentication` and `Microsoft.AspNetCore.Authorization`
in both APIs so failed authorization requirements are visible instead of only the
generic 403 security event. `IncludeErrorDetails` applies to authentication
failures (401), not authorization denials (403); use the server logs for those.
Restart the API processes after changing startup diagnostics or claim mapping.

`UseSecurityEventLogging` emits structured event **1000 / RequestRejected** for
401/403/429 outcomes, with status, **route template** and W3C trace ID. It does not
copy raw URLs/query strings, request bodies, authorization headers or user claims.
The middleware wraps both authorization and rate limiting so early rejections are
visible. Event **1001 / PaymentOwnershipDenied** replaces the console-only ownership
warning; **1002 / HighAmountPayment** replaces the worker's console log of the exact
amount and username. Existing OpenTelemetry logging carries these events to the
configured collector/Aspire dashboard.

The Payments API uses an endpoint filter on `/accounts/{accountId}/payments`
and `/payment` to emit event **1003 / PaymentBindingFailure** when a parameter
check sets a 400 response. For example, an authenticated request to
`/accounts/not-a-number/payments` fails to bind `accountId`. The filter runs
before the handler, records the status, route template and trace ID, and
leaves the response unchanged; it does not log parameter values or tokens.
`ThrowOnBadRequest` is disabled for the Payments API so binding errors reach
filters in Development as well as other environments; this replaces the
Development exception page for those errors throughout that API. The filter
does not cover body-reading failures such as malformed JSON, failures before
endpoint execution, other endpoints, or 400s returned later by the handler.
Binding failures are useful signals, not proof of malicious activity, and
framework logs need their own privacy review.
Filters could already observe binding failures before .NET 11; .NET 11 fixes a
case where pass-through filter factories could let the handler run on failure.

Demonstrate an incorrect scope or account owner and filter for the event IDs.
Tests check trace correlation, query/token omission and the high-amount threshold.
These are operational signals, **not an immutable financial audit trail** or a
fraud decision. Production needs restricted telemetry access, retention, alerting,
bounded ingestion/sampling for floods, and classification/redaction across all
framework logs and telemetry attributes too. Trace IDs are correlation, not
trusted identity or authorization evidence.

## 5. Separate local emulator trust from deployed workload trust

The storage factories accept Aspire connection strings or the implicit Azurite
fallback **only in Development**. Outside Development, a connection string is a
configuration error, not a way to override identity authentication. Endpoints must
be absolute HTTPS URIs without embedded credentials/SAS queries/fragments. Missing
or malformed settings fail explicitly without including their contents in errors.
Development still supports explicit HTTP emulator endpoints.

All three workloads now use the focused credential helper. Only Development tries
Azure CLI/Visual Studio credentials; Staging/Production/custom environment names
require an explicit user-assigned managed identity client ID (`AZURE_CLIENT_ID`).
Deployed clients do not create queues/tables, so provisioning permissions stay out
of workload roles. Azurite auto-creation remains a Development convenience.

Terraform now wires nonsecret client IDs and service endpoints into Container Apps,
sets their ASP.NET Core environment to Production, and attaches the existing ACR
pull identity as well as each workload identity. A client ID selects the workload
identity; it is **not a secret**. Storage access is scoped as follows:

| Workload | Resource | Role |
|---|---|---|
| AccountsApi | accounts table | Storage Table Data Reader |
| PaymentsApi | accounts table | Storage Table Data Reader |
| PaymentsApi | transactions table | Storage Table Data Reader |
| PaymentsApi | payments queue | Storage Queue Data Message Sender |
| Processing | payments queue | Storage Queue Data Message Processor |
| Processing | accounts table | Storage Table Data Reader |
| Processing | transactions table | Storage Table Data Contributor |

This adds the missing PaymentsApi account-read grant and a read-only Processing
grant for execution-time ownership checks; the worker cannot use its account-table
grant to edit ownership. There are no account-wide permissions. Processing uses
the least privilege needed for its account reads. Built-in
Contributor still grants more than append-only ledger access; a reviewed custom
role is a further exercise. Existing broader inherited roles must also be removed
for effective least privilege. Azure role propagation is not instantaneous.

`StorageIdentityTests` covers non-Development connection-string rejection, HTTPS
and configuration boundaries. To exercise the optional live emulator round-trip,
set `MINIBANK_TEST_STORAGE_CONNECTION_STRING` to a **disposable local Azurite**
account and run the test project. The test creates and removes randomly named
test resources; do not point it at real storage.

The storage module also has Terraform mock-provider tests (no Azure resources):

```powershell
terraform -chdir=infrastructure\terraform\modules\storage init -backend=false
terraform -chdir=infrastructure\terraform\modules\storage test
```

**Deployment caveats:** no Azure deployment is performed by these examples.
Inspect the Terraform plan before applying: narrower role scopes replace existing
assignments. The scaffold's public storage endpoint remains enabled. Shared Key
is also deliberately **not** disabled in this commit: the locked AzureRM 4.81
`azurerm_storage_table` resource always uses Shared Key for ACL operations, even
with `storage_use_azuread`. First plan a state-safe migration to ARM/AzAPI table
provisioning (or verify a newer provider), then disable account keys and public
access. Simply flipping those flags here would break the current provisioning
workflow. No workload itself needs an account key.

## Research and further recommendations

Primary references consulted:

- [Microsoft: Develop using Zero Trust principles](https://learn.microsoft.com/security/zero-trust/develop/overview)
- [ASP.NET Core: JWT bearer authentication](https://learn.microsoft.com/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-11.0)
- [ASP.NET Core: Rate limiting](https://learn.microsoft.com/aspnet/core/performance/rate-limit?view=aspnetcore-11.0)
- [OWASP: Input validation](https://cheatsheetseries.owasp.org/cheatsheets/Input_Validation_Cheat_Sheet.html)
- [System.Text.Json: Disallow unmapped members](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/missing-members)
- [OWASP: Logging](https://cheatsheetseries.owasp.org/cheatsheets/Logging_Cheat_Sheet.html)
- [IdentityModel logging and PII](https://learn.microsoft.com/dotnet/api/microsoft.identitymodel.logging.identitymodeleventsource.showpii)
- [Azure: Managed identities best practices](https://learn.microsoft.com/entra/identity/managed-identities-azure-resources/managed-identity-best-practice-recommendations)
- [Azure: Table data RBAC scopes](https://learn.microsoft.com/azure/storage/tables/assign-azure-role-data-access)
- [Azure: Queue data RBAC scopes](https://learn.microsoft.com/azure/storage/queues/assign-azure-role-data-access)
- [Azure: Prevent Shared Key authorization](https://learn.microsoft.com/azure/storage/common/shared-key-authorization-prevent)
- [Locked AzureRM 4.81 table provisioning limitation](https://github.com/hashicorp/terraform-provider-azurerm/blob/v4.81.0/website/docs/r/storage_table.html.markdown)

Useful next demonstrations, intentionally not implied by the samples above:

- Add a restricted quarantine workflow and bounded retry/dead-letter handling for
  rejected messages. Current event 1003 omits payload data and invalid messages
  are deleted after recording the rejection reason.
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

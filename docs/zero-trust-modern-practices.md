# Modern Zero Trust recommendations for MiniBank

These are further recommendations discussed during this session, **not implemented
features**. They complement the
[implemented .NET and infrastructure samples](zero-trust-samples.md) and the
[agentic AI guidance](zero-trust-agentic-ai.md).

Research context: November 10, 2026. Product support, licensing and release status
can change; verify the linked primary documentation before presenting or deploying.

## What .NET 11 changes for this demo

.NET 11 was released on November 10, 2026. Its ASP.NET Core authentication and
authorization updates are useful, but most are not new controls for this
repository's current architecture:

| .NET 11 capability | Relevance here |
|---|---|
| `IAuthorizationRequirementData` metadata is enforced consistently on MVC controllers/actions, SignalR hubs/methods, and Blazor authorization components. | The APIs use Minimal APIs, where this authorization model is already supported. It becomes directly useful if the sample adds those endpoint types; it does not replace the existing fallback policy, exact scope check, or account-level authorization. |
| SignalR can refresh authentication on an established connection. | There are no SignalR hubs or long-lived client connections in this sample. If one is added, token refresh and authorization must be tested together; connection lifetime must not become an authorization lease. |
| Blazor static server-side rendering relies on antiforgery middleware for form validation. | There is no browser-facing form app here. This matters if a cookie-authenticated BFF or Blazor UI is added, not for the current bearer-token APIs. |

These are not substitutes for validating JWT issuer, audience, lifetime and
delegated scopes. The documented .NET 11 updates do not make a valid access token
proof that a payment is still authorized at execution time. For this API/worker
design, the higher-value next steps are to bind ownership to stable Entra tenant
and object IDs, recheck current ownership at the worker boundary, then design
transaction-bound approval and durable idempotency/atomic ledger processing.
The worker checks are defense in depth: a queue producer that can forge messages
remains a trusted boundary until an independent approval record is introduced.

## What was already added

The implementation details, demonstration instructions, limitations and references
are in [Zero Trust demo additions](zero-trust-samples.md). Each addition has its own
local commit:

| Commit | Addition |
|---|---|
| `5f6ff7a` | Deny-by-default API authorization requiring exact delegated scopes |
| `01e1169` | Payment contract and business validation before account access or queue publishing |
| `7067647` | Request size/header/time budgets and bounded per-process rate limiting |
| `7868bf0` | Privacy-safe, correlated security events instead of explicit PII/token logging |
| `0fd1317` | Development-only emulator credentials, explicit deployed workload identities and resource-scoped storage roles |

These samples do not establish production readiness. In particular, worker
revalidation, durable idempotency, atomic ledger updates and fully private storage
remain separate work.

## 1. Bind approval to the exact transaction

Authentication establishes who is calling; it does not prove that the person
approved the exact payment being executed.

For a sensitive transfer, create a server-side pending operation containing the
source account, beneficiary, amount and currency. A trusted interface should show
those details and record approval by an authorized user. Bind approval to that
operation, an expiry and a single-use identifier. Recheck authorization and
relevant business state at execution, and consume approval atomically. Changed
parameters require new approval.

Use durable idempotency and an appropriate transaction/outbox design so retries
cannot duplicate a payment or leave debit and credit partially applied. Approval
and idempotency solve different problems; neither replaces the other.

**Demo:** approve one payment, then change its beneficiary or amount, or replay
its approval. The execution layer must reject the changed or repeated operation.

Reference: [OWASP transaction authorization](https://cheatsheetseries.owasp.org/cheatsheets/Transaction_Authorization_Cheat_Sheet.html).

## 2. Require stronger authentication for sensitive operations

Use Entra Conditional Access authentication context for high-value transfers or
other sensitive actions. The API checks for the required authentication context
and returns a claims challenge when it is missing; the client obtains a token
satisfying the tenant's configured policy before retrying.

This requires a real tenant, suitable licensing, administrator-configured policies
and a client that handles claims challenges. Do not equate logging a high payment
amount with enforcing step-up authentication.

**Important distinction:** stronger authentication is not approval of the exact
transaction. Combine it with transaction-bound authorization where appropriate.

References:
[authentication context](https://learn.microsoft.com/entra/identity-platform/developer-guide-conditional-access-authentication-context)
and [claims challenges](https://learn.microsoft.com/entra/identity-platform/claims-challenge).

## 3. Authorize the resource and its current state

A valid token with `Payment.Create` is necessary, but not sufficient. Continue
checking account ownership and add checks for the current operation: account
status, permitted beneficiary, transaction limits and any required approval.
Revalidate at the worker boundary rather than treating a queued message as proof
of the end user's authority.

Move ownership from mutable `preferred_username` values to the appropriate stable
tenant/object identity (`tid` plus `oid`). This needs an explicit migration of
stored ownership and emulator seed data; do not silently reinterpret old rows.
An authenticated identity must still be mapped to an authorized business account.

**Demo:** a correctly scoped user cannot debit another user's account, and a
previously accepted request cannot execute after the relevant authorization or
business state changes.

Reference: [Entra claims validation](https://learn.microsoft.com/entra/identity-platform/claims-validation).

## 4. Keep infrastructure-only identities out of application code

The Container Apps scaffold attaches both workload identities and an ACR pull
identity. Restrict the registry-only identity's availability to application code
using Container Apps `identitySettings` with lifecycle `None`, while retaining
its platform image-pull use. Give application identities only the lifecycle
availability they need.

Attaching two identities with different roles does not isolate them if compromised
application code can request tokens for both.

**Demo:** the platform can pull the image, while application code cannot acquire
a token for the registry-only identity.

References:
[managed identity availability](https://learn.microsoft.com/azure/container-apps/managed-identity#control-managed-identity-availability)
and the [stable ARM schema](https://learn.microsoft.com/azure/templates/microsoft.app/2025-01-01/containerapps).

## 5. Verify the artifact, not just the repository

Deploy images by immutable digest. Generate provenance attestations and an SBOM,
then verify the artifact's digest, expected repository and trusted build workflow
before deployment. Enforce scanning and a tested update process.

Generating an attestation without verifying it is not an enforcement boundary.
Valid provenance establishes where an artifact came from, not that its code is
safe or free of vulnerabilities.

**Demo:** reject an image built outside the approved workflow even when it has a
plausible tag.

Reference: [GitHub artifact attestations](https://docs.github.com/en/actions/security-for-github-actions/using-artifact-attestations).

## 6. Narrow CI/CD federation trust

Use GitHub Actions OIDC federation instead of stored Azure client secrets, but
constrain the trust relationship to the intended repository, environment or
branch, and trusted workflow where supported. Use immutable owner/repository
identifiers through supported subject customization rather than relying solely
on reusable names.

Give deployment identities only the necessary scopes and protect production
environments. OIDC removes a stored secret; it does not make an overprivileged
deployment identity safe. Inspect the repository's actual OIDC subject format;
newer defaults do not necessarily apply to older repositories.

Reference: [GitHub OIDC security hardening](https://docs.github.com/en/actions/security-for-github-actions/security-hardening-your-deployments/about-security-hardening-with-openid-connect).

## Optional architectural extensions

### Browser token isolation with a BFF

If browser token exposure is part of the session, consider a backend-for-frontend
(BFF). Keep downstream access tokens server-side and use a secured browser session
cookie with CSRF protection. This reduces exposure of bearer tokens to browser
JavaScript, but does not eliminate XSS, session abuse or the need for API
authorization. It is an architectural change, not a token-storage toggle.

### Regional managed identity isolation

For multi-region deployments, assess regional isolation for user-assigned managed
identities and use separate identities per region where appropriate.

This constrains the identity's association with **source compute resources**.
It does not restrict the region of target storage, replace RBAC, or prevent replay
of an already stolen bearer token.

Reference: [managed identity isolation scope](https://learn.microsoft.com/entra/identity/managed-identities-azure-resources/managed-identities-isolation-scope).

## Avoid overstating token protection

- **Continuous Access Evaluation (CAE):** declaring `cp1` or implementing claims
  challenges does not automatically make a custom API a CAE-enabled resource.
  Support depends on the client/resource combination. Workload-identity CAE is
  documented for Microsoft Graph and excludes managed identities; do not present
  it as instant revocation for this demo's managed-identity storage calls.
- **DPoP and Entra Token Protection:** sender-constrained tokens are valuable,
  but require compatible issuance, clients and resource validation. Do not claim
  Entra Token Protection universally covers custom ASP.NET Core APIs or that
  bearer JWT validation alone provides proof of possession.
- **Revocation:** disabling an identity or preventing new tokens is not proof
  that every issued access token immediately stops working. Design sensitive
  operations around server-side authorization and explicit execution controls.

References:
[CAE](https://learn.microsoft.com/entra/identity/conditional-access/concept-continuous-access-evaluation),
[workload CAE](https://learn.microsoft.com/entra/identity/conditional-access/concept-continuous-access-evaluation-workload),
[Token Protection](https://learn.microsoft.com/entra/identity/conditional-access/concept-token-protection)
and [DPoP, RFC 9449](https://www.rfc-editor.org/rfc/rfc9449.html).

## .NET 11 references

- [.NET 11 release announcement](https://devblogs.microsoft.com/dotnet/announcing-dotnet-11/)
- [What's new in ASP.NET Core 11](https://learn.microsoft.com/aspnet/core/release-notes/aspnetcore-11?view=aspnetcore-11.0)
- [Custom authorization policies with `IAuthorizationRequirementData`](https://learn.microsoft.com/aspnet/core/security/authorization/custom-authorization-policies-with-iauthorizationrequirementdata?view=aspnetcore-11.0)
- [SignalR authentication and authorization](https://learn.microsoft.com/aspnet/core/signalr/authn-and-authz?view=aspnetcore-11.0)
- [ASP.NET Core antiforgery guidance](https://learn.microsoft.com/aspnet/core/security/anti-request-forgery?view=aspnetcore-11.0)

## Suggested teaching priority

Start with transaction-bound approval, resource/state authorization and step-up
authentication. They show why "valid token" does not mean "authorized payment."
Then demonstrate identity availability isolation and verified artifact provenance
to extend Zero Trust beyond the HTTP request into workloads and deployment.

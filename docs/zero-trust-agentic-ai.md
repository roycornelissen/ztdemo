# Zero Trust for agentic AI

Agentic AI makes Zero Trust more important, not less: you are giving a
probabilistic system the ability to exercise real privileges.

> **The model proposes. Trusted application code authorizes. A constrained tool executes.**
>
> Design so that even a successfully manipulated model cannot perform an
> unauthorized action.

This document provides session guidance and a proposed MiniBank demonstration.
It does not describe an agent implementation already present in this repository.
For the implemented .NET samples, see [Zero Trust samples](zero-trust-samples.md).
For further API, identity and supply-chain recommendations, see
[Modern Zero Trust practices](zero-trust-modern-practices.md).

## Translate the three principles into agent design

| Zero Trust principle | What it means for an AI agent |
|---|---|
| **Verify explicitly** | Authenticate the caller and agent; authorize every tool invocation against the actual resource and action. A retrieved document or another agent's message cannot grant permission. |
| **Use least privilege** | Give each agent only the tools, data, destinations and temporary privileges needed for its task, not the developer's credentials or a general-purpose admin account. |
| **Assume breach** | Assume hostile content can influence the model. Bound the resulting damage through isolated execution, egress restrictions, approval gates, budgets and recovery controls. |

This closely matches Microsoft's
[least-privilege guidance for AI agents](https://learn.microsoft.com/security/zero-trust/sfi/least-privilege-for-ai-agents).

## Seven practical rules worth teaching

### 1. Treat everything the agent reads as potentially hostile

Prompt injection can arrive through an invoice, email, webpage, repository file,
tool response or another agent, not just the chat prompt. Keep instructions and
retrieved content separate, preserve provenance, and never turn "the document
says the user approved this" into authorization.

System prompts, delimiters and injection detectors help, but are **not security
boundaries**.

### 2. Make permissions smaller than the agent's imagination

Prefer narrow tools such as `GetMyAccounts` and `DraftPayment` over `ExecuteSql`,
unrestricted HTTP requests or shell access. Validate arguments and enforce
ownership, limits and allowed state transitions **inside the tool/API**,
regardless of what the model claims. Schema-valid JSON proves structure, not
permission or safety.

Keep credentials outside model context. Give agents dedicated identities with
accountable owners and distinguish **acting for a user** from **acting
autonomously**. A broad application identity must not silently substitute for a
user's limited authority.

### 3. Put approval outside the conversation

For payments, production deployments, privilege changes or bulk deletion, the
agent should propose, not self-approve. A trusted interface should show the exact
target and parameters. Bind approval to the authenticated approver, exact
operation, expiry and a single-use identifier; verify and consume it atomically
at execution.

A model-generated `approved: true`, an email claiming approval, or a remembered
"always allow" is not sufficient. Avoid approval fatigue by automating genuinely
low-risk work and reserving meaningful gates for consequential actions.

See [OWASP's agent security guidance](https://cheatsheetseries.owasp.org/cheatsheets/AI_Agent_Security_Cheat_Sheet.html).

### 4. Secure combinations of tools, not just individual tools

A read-only document tool plus an unrestricted email or HTTP tool creates an
exfiltration path. Restrict outbound destinations and recipients, minimize
returned data, and isolate code execution from production credentials, host files
and cloud metadata endpoints.

"Read-only" does not mean harmless when sensitive data can leave through another
channel.

### 5. Treat memory and delegation as new trust boundaries

Apply user/tenant authorization **before retrieving** RAG documents, not after
placing them in the model's context. Isolate conversations, caches and persistent
memory; track their sources and expiry. Do not let remembered text change
security policy.

Likewise, a researcher agent must not gain payment authority simply by asking an
executor agent. Authenticate each hop, preserve the initiating user/task context,
and permit only explicitly authorized delegation. A signed message proves origin,
not permission.

### 6. Don't confuse MCP connectivity with security

The Model Context Protocol (MCP) is an integration protocol, not an automatic
trust layer. Review servers, packages, tool definitions and updates as software
dependencies. Authorize tool execution even if discovery hides unavailable tools.

For protected HTTP MCP servers, validate tokens intended for that server and
acquire appropriate downstream tokens. Do not forward an incoming bearer token
indiscriminately. The
[MCP security guidance explicitly forbids token passthrough](https://modelcontextprotocol.io/docs/draft/tutorials/security/security_best_practices).

Local stdio tools have a different boundary: launching one is executing software
with whatever host permissions and credentials it receives.

### 7. Bound autonomy and measure containment

Enforce maximum tool calls, elapsed time, cost, retries, delegation depth and
transaction value outside the model. Provide a kill switch that stops further
execution, not merely the chat UI.

Record agent/user identity, tool, resource, policy decision, approval reference
and outcome without indiscriminately logging prompts, tokens or sensitive
documents.

Test hostile retrieved content, cross-user retrieval, forged approval, changed
payment parameters, duplicate execution and runaway delegation. The useful
measure is **whether unauthorized effects were prevented**, not whether the model
said it detected an attack.

## A memorable MiniBank demonstration

Give an assistant an invoice containing an embedded instruction to change the
beneficiary and send account information elsewhere. Then show:

1. The assistant can extract invoice data and draft a payment.
2. The execution layer rejects an unapproved beneficiary or destination, even if
   the model requests it.
3. A person approves the exact payment through a trusted interface; changing its
   details or replaying approval fails.

The lesson is stronger than "our prompt filter caught it": **the system remains
constrained even when the model gets it wrong.**

## Microsoft-specific examples

Dedicated [Entra agent identities](https://learn.microsoft.com/entra/agent-id/what-is-microsoft-entra-agent-id)
and Foundry Prompt Shields are useful implementation examples, subject to
licensing and feature support.

[Prompt Shields](https://learn.microsoft.com/azure/foundry/openai/concepts/content-filter-prompt-shields)
provide detection and mitigation, not authorization or proof that content is
trustworthy. Some tool-response protections remain preview. Product availability
and licensing change; check the linked documentation before presenting.

## Closing message

> **Never let text become authority. Every agent action must pass the same
> identity, authorization and business controls as any other application.**

---
'@wayd/mcp': minor
---

Add status transition tools, and tool annotations so clients confirm before running them.

Fourteen transitions are now reachable: portfolio activate / close / archive, program activate / complete / cancel, project approve / activate / complete / cancel, and strategic initiative approve / activate / complete / cancel. Each takes a UUID and no body, matching the API's dedicated action endpoints — status is never set by writing a field.

These are the first tools that change a published status other people rely on, so the server now emits the MCP `ToolAnnotations` that clients use to decide what to confirm. Every transition is marked `destructiveHint`, as are the eleven existing delete and remove tools across story maps, tasks, and KPI measurements, which previously carried no hint at all and so would run without a prompt. GET-backed tools are advertised `readOnlyHint`; any tool that is not a GET must opt in explicitly, so a future write tool can never be silently advertised as safe. A protocol-level test asserts the contract over the wire and fails with a named tool if a hint is ever dropped.

The annotation is advisory — it tells a client to ask and cannot force one to. Server-side authorization is unchanged and remains the actual control: portfolio, program, and project transitions still require delivery leadership (Owner or Manager on the record or an ancestor), while initiative transitions check the permission claim only.

Two side effects are documented on the tools themselves and in the `wayd-ppm` skill, because neither is visible from the endpoint name. **Activating a portfolio sets its start date to today and closing it sets its end date to today**, with no way to backdate through these calls — so they must not be used to tidy up a record that really started or ended on another date. And **completing or cancelling a strategic initiative closes it**, after which its KPIs and linked projects can no longer be modified.

The skill also documents the prerequisites that cause rejections, most notably that a program cannot be completed or cancelled from Active until every project inside it is already closed.

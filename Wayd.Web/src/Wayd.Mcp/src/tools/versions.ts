import type { McpToolDefinition } from '../types.js';

/**
 * Versions — one artifact that was built, as distinct from what was announced.
 *
 * A **version** is a cut of one releasable product (`Wayd API 4.12.0`); a **release** is the
 * announcement made to customers (`Wayd 2026.09`). They share most of their field names and both
 * have a "version" string, so every description here says which one it means. An agent picking
 * between Versions_Plan and Releases_Plan has only this text to go on.
 *
 * A version describes what was cut, never where it went: a version with no deployment is a complete
 * record, which is what makes it possible to enter history by hand before any pipeline is wired up.
 */

/** Shared annotation: a human must approve before the record changes. */
const requiresConfirmation = {
  destructiveHint: true,
  readOnlyHint: false,
  idempotentHint: false,
} as const;

/** Shared annotation: reads only, safe to run without asking. */
const readsOnly = {
  readOnlyHint: true,
  destructiveHint: false,
  idempotentHint: true,
} as const;

const NOT_A_RELEASE =
  'A version is one artifact that was built, not the announcement made to customers — for that, use the Releases_* tools instead.';

const ID_ONLY = 'Version ID. This endpoint takes a UUID only, not a version key.';

export const definitions: [string, McpToolDefinition][] = [

  ['Versions_GetVersions', {
    name: 'Versions_GetVersions',
    description: `List versions — the artifacts that were built, such as \`Wayd API 4.12.0\`. ${NOT_A_RELEASE} Everything not yet shipped comes first, then what has shipped, newest first: what is still coming is usually what you are looking for. Never ordered by the version number, which is free text and never parsed — \`4.8.2\` and \`2026.04\` are both just labels. There is no package filter here: a version carries no pointer to the package it shipped in, so ask that question from the packages side with ReleasePackages_GetReleasePackages.`,
    inputSchema: {"type":"object","properties":{"productId":{"type":"string","format":"uuid","description":"Only versions cut against this product."},"statusCategory":{"type":"array","items":{"type":"integer"},"description":"Filter by status category rather than status name, since names are per-organization."}},"required":[]},
    method: 'get',
    pathTemplate: '/api/product-management/versions',
    executionParameters: [{"name":"productId","in":"query"},{"name":"statusCategory","in":"query"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'List versions', ...readsOnly },
  }],

  ['Versions_GetVersion', {
    name: 'Versions_GetVersion',
    description: `Get one version in full — its product, version number, and its target, cut and released dates. ${NOT_A_RELEASE} Accepts the version's UUID or its short key.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string","description":"Version ID (UUID) or its short key."}},"required":["idOrKey"]},
    method: 'get',
    pathTemplate: '/api/product-management/versions/{idOrKey}',
    executionParameters: [{"name":"idOrKey","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Get version', ...readsOnly },
  }],

  ['Versions_GetStatusHistory', {
    name: 'Versions_GetStatusHistory',
    description: `Get a version's status change history, newest first — when it was cut, how long it sat ready before shipping, who moved it. Each entry reports the status names as they were at the time, so a status renamed since does not rewrite the past. Correcting a date leaves this untouched.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string","description":"Version ID (UUID) or its short key."}},"required":["idOrKey"]},
    method: 'get',
    pathTemplate: '/api/product-management/versions/{idOrKey}/status-history',
    executionParameters: [{"name":"idOrKey","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Get version status history', ...readsOnly },
  }],

  ['Versions_Plan', {
    name: 'Versions_Plan',
    description: `Record a version against a product. ${NOT_A_RELEASE} The product is required and **must be of a releasable type** — a version is a cut of something that ships, so the API refuses a product line or other non-releasable node. Only the target date is set here; cutting and releasing are their own actions, because each records something that happened and each carries its own rule.`,
    inputSchema: {"type":"object","properties":{"requestBody":{"type":"object","properties":{"productId":{"type":"string","format":"uuid","description":"The product this version is cut against. Must be a releasable product type."},"version":{"type":"string","description":"The version number — 4.8.2, 2026.04, v3-beta. Free text, never parsed or compared."},"name":{"type":"string","description":"An optional human name."},"targetDate":{"type":"string","format":"date","description":"When the version is aimed at. Format YYYY-MM-DD."},"sequence":{"type":"integer","format":"int64","description":"A manual ordering override, for a backport that ships after the version superseding it."}},"required":["productId","version"]}},"required":["requestBody"]},
    method: 'post',
    pathTemplate: '/api/product-management/versions',
    executionParameters: [],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Plan a version', ...requiresConfirmation },
  }],

  ['Versions_Update', {
    name: 'Versions_Update',
    description: `Update a version's descriptive fields. **This is a whole-record overwrite: an omitted field is cleared.** Send every value the version should end up with, including ones you are not changing. The dates are not here — each carries a rule the aggregate enforces, and folding them into a blanket save would hide which rule refused.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":ID_ONLY},"requestBody":{"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"Must match the id in the path."},"version":{"type":"string","description":"The version number. Free text, never parsed."},"name":{"type":"string","description":"Cleared when omitted."},"notes":{"type":"string","description":"Engineering notes on what changed in the artifact. Cleared when omitted. Distinct from a release's notes, which are written for customers."},"sequence":{"type":"integer","format":"int64","description":"Cleared when omitted."}},"required":["id","version"]}},"required":["id","requestBody"]},
    method: 'put',
    pathTemplate: '/api/product-management/versions/{id}',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Update a version', ...requiresConfirmation },
  }],

  ['Versions_Cut', {
    name: 'Versions_Cut',
    description: `Record that a version was cut — scope is frozen and it is ready to ship. **One-way: a version cannot be cut twice**, and a released or withdrawn version refuses it. Cutting is not a prerequisite for releasing: a version imported after the fact can be marked released without ever having been cut, which is why this is a separate action rather than a step.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":ID_ONLY},"requestBody":{"type":"object","properties":{"cutDate":{"type":"string","format":"date","description":"The date it was cut. Format YYYY-MM-DD."}},"required":["cutDate"]}},"required":["id","requestBody"]},
    method: 'post',
    pathTemplate: '/api/product-management/versions/{id}/cut',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Cut a version', ...requiresConfirmation },
  }],

  ['Versions_MarkReleased', {
    name: 'Versions_MarkReleased',
    description: `Record that a version shipped. **This is not announcing it to customers** — that is Releases_MarkReleased on a release. A version can be marked released without ever having been cut, which is what makes importing historical versions possible; where it was cut, the released date cannot be earlier than the cut date. Refused on a withdrawn version.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":ID_ONLY},"requestBody":{"type":"object","properties":{"releasedDate":{"type":"string","format":"date","description":"The date it shipped. Format YYYY-MM-DD. Cannot be earlier than the cut date where one is recorded."}},"required":["releasedDate"]}},"required":["id","requestBody"]},
    method: 'post',
    pathTemplate: '/api/product-management/versions/{id}/release',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Mark a version released', ...requiresConfirmation },
  }],

  ['Versions_Withdraw', {
    name: 'Versions_Withdraw',
    description: `Pull a version. Terminal. **A released version can still be withdrawn** — pulling something after it shipped is exactly the case this exists for — but a withdrawn one cannot be released. Use this only when a real version was pulled; if it was marked released by mistake and never actually shipped, use Versions_Revert instead.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":ID_ONLY},"requestBody":{"type":"object","properties":{"reason":{"type":"string","description":"Why it was pulled. Optional — recorded on the status transition where given."}},"required":[]}},"required":["id","requestBody"]},
    method: 'post',
    pathTemplate: '/api/product-management/versions/{id}/withdraw',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Withdraw a version', ...requiresConfirmation },
  }],

  ['Versions_Revert', {
    name: 'Versions_Revert',
    description: `Record that a version marked as shipped did **not in fact ship** — the wrong record was updated. Returns it to Ready, or to the initial status where it was never cut, and clears the released date. A reason is required, unlike a withdrawal's optional one: this contradicts something the append-only history already asserts. Do not use this for a version that really shipped and was then pulled — that is Versions_Withdraw.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":ID_ONLY},"requestBody":{"type":"object","properties":{"reason":{"type":"string","description":"Why the version was reverted. Required."}},"required":["reason"]}},"required":["id","requestBody"]},
    method: 'post',
    pathTemplate: '/api/product-management/versions/{id}/revert',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Revert a version', ...requiresConfirmation },
  }],

  ['Versions_CorrectDates', {
    name: 'Versions_CorrectDates',
    description: `Fix a version's target, cut or released date that was recorded wrongly. The status does not move and the status history is left untouched — that is the point of having this separate from Versions_Cut and Versions_MarkReleased, which assert the version moved and refuse to run twice. **All three dates are sent, so an omitted target or cut date is cleared.** The released date cannot be cleared once set: a released record with no released date contradicts its own status — revert it instead. The released date cannot precede the cut date. Refused on a withdrawn version.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":ID_ONLY},"requestBody":{"type":"object","properties":{"targetDate":{"type":"string","format":"date","description":"Format YYYY-MM-DD. Omit to clear it."},"cutDate":{"type":"string","format":"date","description":"Format YYYY-MM-DD. Omit to clear it. Commonly filled in afterwards, since a version can ship without ever being cut."},"releasedDate":{"type":"string","format":"date","description":"Format YYYY-MM-DD. Cannot be cleared once set, and cannot precede the cut date."}},"required":[]}},"required":["id","requestBody"]},
    method: 'put',
    pathTemplate: '/api/product-management/versions/{id}/dates',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Correct version dates', ...requiresConfirmation },
  }],

  ['Versions_MoveTargetDate', {
    name: 'Versions_MoveTargetDate',
    description: `Move or clear a version's target date. Omitting the date records that the version is no longer targeted, which is a different statement from never having set one. Refused on a released or withdrawn version.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":ID_ONLY},"requestBody":{"type":"object","properties":{"targetDate":{"type":"string","format":"date","description":"The new target. Format YYYY-MM-DD. Omit to clear it."}},"required":[]}},"required":["id","requestBody"]},
    method: 'put',
    pathTemplate: '/api/product-management/versions/{id}/target-date',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Move a version target date', ...requiresConfirmation },
  }],

];

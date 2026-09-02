import type { McpToolDefinition } from '../types.js';

/**
 * Product releases — what was announced to customers.
 *
 * The word "release" means three things in most organizations, and Wayd keeps them apart. A
 * **release** is the announcement (`Wayd 2026.09`); a **version** is one artifact that was built
 * (`Wayd API 4.12.0`); a **package** is what moved through environments together. Every description
 * here repeats that distinction, because an agent choosing between these tools has nothing else to
 * go on — it cannot see the screen, and the two records share most of their field names.
 *
 * Mutations are annotated `destructiveHint` so clients confirm before running. That matters more
 * here than elsewhere: announcing a release is a statement to customers, and withdrawing one
 * retracts a statement already made.
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

const NOT_A_VERSION =
  'A release is what was announced to customers, not what was built — for one artifact and its version number, use the Versions_* tools instead.';

const ID_ONLY = 'Release ID. This endpoint takes a UUID only, not a release key.';

export const definitions: [string, McpToolDefinition][] = [

  ['Releases_GetReleases', {
    name: 'Releases_GetReleases',
    description: `List product releases — the announcements made to customers, such as \`Wayd 2026.09\`. ${NOT_A_VERSION} Unannounced releases come first, then the most recently announced. Never ordered by the version label, which is free text and never parsed. Filtering by product deliberately excludes releases that name no product: one spanning product lines belongs to no single product, so listing it under one would misstate what that product announced.`,
    inputSchema: {"type":"object","properties":{"productId":{"type":"string","format":"uuid","description":"Only releases announced under this product. Excludes releases with no product."},"statusCategory":{"type":"array","items":{"type":"integer"},"description":"Filter by status category rather than status name, since names are per-organization."},"containingVersionId":{"type":"string","format":"uuid","description":"Only releases that announced this version, by either route — carried directly, or shipped inside one of the release's packages. This is how to answer \"where was this version announced?\"."}},"required":[]},
    method: 'get',
    pathTemplate: '/api/product-management/releases',
    executionParameters: [{"name":"productId","in":"query"},{"name":"statusCategory","in":"query"},{"name":"containingVersionId","in":"query"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'List releases', ...readsOnly },
  }],

  ['Releases_GetRelease', {
    name: 'Releases_GetRelease',
    description: `Get one release in full, including everything it announces: the packages it shipped and the versions it carries directly. Each contents entry reports its own shipped date, which is what tells you whether the release can be announced yet. ${NOT_A_VERSION} Accepts the release's UUID or its short key.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string","description":"Release ID (UUID) or its short key."}},"required":["idOrKey"]},
    method: 'get',
    pathTemplate: '/api/product-management/releases/{idOrKey}',
    executionParameters: [{"name":"idOrKey","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Get release', ...readsOnly },
  }],

  ['Releases_GetStatusHistory', {
    name: 'Releases_GetStatusHistory',
    description: `Get a release's status change history, newest first. Each entry reports the status names as they were at the time, so a status renamed since does not rewrite the past. Correcting a date leaves this untouched — that is the point of having a separate action for it.`,
    inputSchema: {"type":"object","properties":{"idOrKey":{"type":"string","description":"Release ID (UUID) or its short key."}},"required":["idOrKey"]},
    method: 'get',
    pathTemplate: '/api/product-management/releases/{idOrKey}/status-history',
    executionParameters: [{"name":"idOrKey","in":"path"}],
    requestBodyContentType: undefined,
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Get release status history', ...readsOnly },
  }],

  ['Releases_Plan', {
    name: 'Releases_Plan',
    description: `Draft a release — the announcement, before it carries anything. ${NOT_A_VERSION} Contents are attached afterwards with Releases_SetContents, because an announcement is commonly drafted before anyone knows which versions will make it. The product is optional and usually names a product *line* rather than a leaf; leave it empty for a release spanning product lines. Unlike a version, a release is not restricted to releasable product types.`,
    inputSchema: {"type":"object","properties":{"requestBody":{"type":"object","properties":{"productId":{"type":"string","format":"uuid","description":"The product to announce under. Optional, and typically a product line. Omit for a release spanning product lines — but note such a release is excluded when releases are filtered by product."},"version":{"type":"string","description":"The release as your organization announces it — 2026.07, Spring Release, R4. Free text, never parsed. This is the announcement's own label, not the version number of anything inside it."},"name":{"type":"string","description":"An optional human name."},"targetDate":{"type":"string","format":"date","description":"When the release is expected to be announced. Format YYYY-MM-DD."},"sequence":{"type":"integer","format":"int64","description":"A manual ordering override, for the rare case where chronology misleads."}},"required":["version"]}},"required":["requestBody"]},
    method: 'post',
    pathTemplate: '/api/product-management/releases',
    executionParameters: [],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Plan a release', ...requiresConfirmation },
  }],

  ['Releases_Update', {
    name: 'Releases_Update',
    description: `Update a release's descriptive fields. **This is a whole-record overwrite: an omitted field is cleared.** Send every value the release should end up with, including ones you are not changing — omitting the product makes the release span product lines, and omitting the notes deletes them. The dates and the contents are not here; each has its own tool because each carries a rule this one does not.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":ID_ONLY},"requestBody":{"type":"object","properties":{"id":{"type":"string","format":"uuid","description":"Must match the id in the path."},"version":{"type":"string","description":"The release's own label. Free text, never parsed."},"name":{"type":"string","description":"Cleared when omitted."},"notes":{"type":"string","description":"Product notes, written for customers. Cleared when omitted."},"productId":{"type":"string","format":"uuid","description":"Cleared when omitted, which makes the release span product lines."},"sequence":{"type":"integer","format":"int64","description":"Cleared when omitted."}},"required":["id","version"]}},"required":["id","requestBody"]},
    method: 'put',
    pathTemplate: '/api/product-management/releases/{id}',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Update a release', ...requiresConfirmation },
  }],

  ['Releases_SetContents', {
    name: 'Releases_SetContents',
    description: `Set everything a release announces — the packages it shipped and the versions it carries directly — in one call. **This is a whole-set replacement of both routes: anything left out is removed, and sending two empty lists clears the release entirely.** Read the release first and send back the full intended result, not just what you are adding.

Contents arrive two ways and a release may use both: packages (the usual route, since a package is the deployment unit) and versions carried directly (for a single artifact that shipped alone, where nobody assembled a package).

**A version shipping inside one of the supplied packages cannot also be carried directly** — that would announce the same shipment twice. The rule is judged against what the release ends up containing, so moving a version out of the direct list and into a package that ships it is allowed in this one call. A manifest line naming no version record covers nothing and never conflicts.

An empty release is legitimate rather than a draft: a repackaging or a pricing change is announced with nothing deployed. Contents freeze once the release is announced or withdrawn.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":ID_ONLY},"requestBody":{"type":"object","properties":{"versionIds":{"type":"array","items":{"type":"string","format":"uuid"},"description":"Every version this release carries directly, outside any package. Send the complete set — omitted ids are removed."},"packageIds":{"type":"array","items":{"type":"string","format":"uuid"},"description":"Every package this release shipped. Send the complete set — omitted ids are removed."}},"required":["versionIds","packageIds"]}},"required":["id","requestBody"]},
    method: 'put',
    pathTemplate: '/api/product-management/releases/{id}/contents',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Set what a release announces', ...requiresConfirmation },
  }],

  ['Releases_MarkReleased', {
    name: 'Releases_MarkReleased',
    description: `Record that a release was announced to customers. **Refused while the release carries a version or package that has not shipped** — telling customers a release is out while something inside it has not gone anywhere is the one claim a release can make that its own contents contradict. Call Releases_GetRelease first and check each contents entry's shipped date; release the outstanding ones, or remove them from this release. An empty release announces normally. Shipping and announcing are separate acts, so this date is commonly later than the date the contents shipped.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":ID_ONLY},"requestBody":{"type":"object","properties":{"releasedDate":{"type":"string","format":"date","description":"The date customers were told. Format YYYY-MM-DD. Supplied rather than taken from the clock, because announcements are often recorded after the fact."}},"required":["releasedDate"]}},"required":["id","requestBody"]},
    method: 'post',
    pathTemplate: '/api/product-management/releases/{id}/release',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Announce a release', ...requiresConfirmation },
  }],

  ['Releases_Withdraw', {
    name: 'Releases_Withdraw',
    description: `Retract a release after it was announced. Terminal, and it says **nothing about the versions it carried** — an artifact that shipped has shipped whatever the market was later told, so a version that was itself pulled is withdrawn on its own record. Use this only when a real announcement was retracted; if the release was marked announced by mistake and never actually went out, use Releases_Revert instead.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":ID_ONLY},"requestBody":{"type":"object","properties":{"reason":{"type":"string","description":"Why it was retracted. Optional — recorded on the status transition where given."}},"required":[]}},"required":["id","requestBody"]},
    method: 'post',
    pathTemplate: '/api/product-management/releases/{id}/withdraw',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Withdraw a release', ...requiresConfirmation },
  }],

  ['Releases_Revert', {
    name: 'Releases_Revert',
    description: `Record that a release marked as announced was **not in fact announced** — the wrong record was updated, and it never went out. Returns the release to a live status and clears its announced date. A reason is required, unlike a withdrawal's optional one: this contradicts something the append-only history already asserts, so the record has to say why. Do not use this for a release that really was announced and then retracted — that is Releases_Withdraw.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":ID_ONLY},"requestBody":{"type":"object","properties":{"reason":{"type":"string","description":"Why the release was reverted. Required."}},"required":["reason"]}},"required":["id","requestBody"]},
    method: 'post',
    pathTemplate: '/api/product-management/releases/{id}/revert',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Revert a release', ...requiresConfirmation },
  }],

  ['Releases_CorrectDates', {
    name: 'Releases_CorrectDates',
    description: `Fix a release's target or announced date that was recorded wrongly. The status does not move and the status history is left untouched — that is the point of having this separate from Releases_MarkReleased, which asserts the release moved and refuses to run twice. **Both dates are sent, so an omitted target date is cleared.** The announced date cannot be cleared once set: an announced release with no announced date contradicts its own status — revert it instead. There is no cut date to correct; a release is never cut.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":ID_ONLY},"requestBody":{"type":"object","properties":{"targetDate":{"type":"string","format":"date","description":"When the release was aimed at. Format YYYY-MM-DD. Omit to clear it."},"releasedDate":{"type":"string","format":"date","description":"When it was announced. Format YYYY-MM-DD. Cannot be cleared once set."}},"required":[]}},"required":["id","requestBody"]},
    method: 'put',
    pathTemplate: '/api/product-management/releases/{id}/dates',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Correct release dates', ...requiresConfirmation },
  }],

  ['Releases_MoveTargetDate', {
    name: 'Releases_MoveTargetDate',
    description: `Move or clear a release's target date. Omitting the date records that the release is no longer targeted, which is a different statement from never having set one. Refused on a release in a terminal status.`,
    inputSchema: {"type":"object","properties":{"id":{"type":"string","format":"uuid","description":ID_ONLY},"requestBody":{"type":"object","properties":{"targetDate":{"type":"string","format":"date","description":"The new target. Format YYYY-MM-DD. Omit to clear it."}},"required":[]}},"required":["id","requestBody"]},
    method: 'put',
    pathTemplate: '/api/product-management/releases/{id}/target-date',
    executionParameters: [{"name":"id","in":"path"}],
    requestBodyContentType: 'application/json',
    securityRequirements: [{"ApiKey":[]}],
    annotations: { title: 'Move a release target date', ...requiresConfirmation },
  }],

];

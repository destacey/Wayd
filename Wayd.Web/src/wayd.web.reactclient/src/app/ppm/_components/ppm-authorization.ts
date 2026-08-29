/**
 * Whether the signed-in user may act on a PPM record.
 *
 * Mutating a project, program or portfolio takes a permission claim AND
 * delivery leadership on the record — Owner or Manager on it or an ancestor.
 * The server computes the leadership half, so the UI cannot drift from the
 * rule the aggregate enforces; the claim half gates reaching the endpoint at
 * all. Neither substitutes for the other, and each action pairs the leadership
 * flag with its own claim: Delete is not granted by the Update claim.
 *
 * Strategic initiatives are deliberately not covered — they carry no
 * leadership flag and gate on the claim alone.
 */
export const canActOnPpmRecord = (
  /** The action's own permission claim, e.g. `Permissions.Projects.Delete`. */
  hasClaim: boolean,
  /** The record's server-computed leadership flag, absent while it loads. */
  canManageRecord: boolean | undefined,
) => hasClaim && !!canManageRecord

export default canActOnPpmRecord

/**
 * Sent in the initialize result so every client learns the rules that span all tools,
 * including clients that never load the `skills/` guidance (Claude Desktop, Cursor, ChatGPT,
 * claude.ai connectors). Keep it to these cross-tool rules; per-area guidance belongs in
 * tool descriptions and skills, since clients may place this text in every prompt.
 */
export const SERVER_INSTRUCTIONS = [
  'Wayd tools read and change live delivery data that other people rely on.',
  'Updates are whole-record overwrites, not patches: read the record first and pass back every field that should stay, because an omitted field is cleared.',
  'Role lists (sponsorIds, ownerIds, managerIds, memberIds) replace that role\'s membership, and an omitted or empty list removes everyone in it.',
  'A parameter named idOrKey (or xxxIdOrKey) accepts a UUID or a key; any other id parameter takes a UUID only unless its description says it accepts a key, so look the UUID up with a read tool first.',
  'Status changes, deletes and other destructive tools must have the user\'s explicit confirmation before you call them.',
].join(' ');

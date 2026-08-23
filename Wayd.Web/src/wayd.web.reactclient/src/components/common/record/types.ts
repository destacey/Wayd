export interface RecordSection {
  /**
   * Stable identifier. This appears in the URL as `?section={id}`, so it is a
   * public contract — renaming one breaks links people have already shared.
   * Use kebab-case (`work-items`, `cycle-time`).
   */
  id: string
  /** Display name. Safe to change; the id is what must stay stable. */
  label: string
  /** Optional count shown beside the label in the rail. */
  count?: number
}

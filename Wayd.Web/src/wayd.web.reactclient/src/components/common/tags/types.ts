/**
 * The shape a categorised-tag consumer supplies.
 *
 * Deliberately not tied to any module's DTOs: the components in this folder take
 * these, and each consuming area maps its own types across. That is what lets a
 * second area adopt curated tags without touching anything here.
 *
 * Free-text tags — Azure DevOps work item tags, for instance — are a different
 * model with no axes and no ids, and are not served by these types.
 */

/** A tag a record can carry. */
export interface TagOption {
  id: string
  name: string
  description?: string
  /**
   * Whether the tag can still be applied. An inactive tag stays visible on the
   * records already carrying it — deactivating retires it from new use rather
   * than removing it.
   */
  isActive: boolean
}

/** An axis records are labelled along — Platform, Tech Stack, Compliance. */
export interface TagCategory {
  id: string
  name: string
  description?: string
  /**
   * Whether a record can carry several tags from this axis. False means a second
   * choice replaces the first.
   */
  allowsMany: boolean
  tags: TagOption[]
}

/** A tag a record currently carries, and the axis it came from. */
export interface TagAssignment {
  tagId: string
  tagName: string
  categoryId: string
  categoryName: string
}

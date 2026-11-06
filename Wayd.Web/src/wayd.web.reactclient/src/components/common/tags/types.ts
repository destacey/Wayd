/**
 * The shape a tag consumer supplies.
 *
 * Deliberately not tied to any module's DTOs: the components in this folder take
 * these, and each consuming area maps its own types across. That is what lets a
 * second area adopt curated tags without touching anything here.
 *
 * Two kinds of tag share these types: curated tags in a category, and free tags —
 * informal labels in no category, typed in while tagging. Both have ids. Azure DevOps
 * work item tags, plain strings with no ids, are a different model and are not
 * served here.
 */

/** A tag a record can carry. */
export interface TagOption {
  id: string
  name: string
  description?: string
  /**
   * Whether the tag can still be applied. An inactive tag stays visible on the
   * records already carrying it — deactivating retires it from new use rather
   * than removing it. Always true for a free tag.
   */
  isActive: boolean
}

/** A category of tags records are grouped by — Platform, Tech Stack, Compliance. */
export interface TagCategory {
  id: string
  name: string
  description?: string
  /**
   * Whether a record can carry several tags from this category. False means a second
   * choice replaces the first.
   */
  allowsMany: boolean
  tags: TagOption[]
}

/**
 * A tag a record currently carries, and the category it came from. A free tag has
 * no category, so both category fields are absent.
 */
export interface TagAssignment {
  tagId: string
  tagName: string
  categoryId?: string
  categoryName?: string
}

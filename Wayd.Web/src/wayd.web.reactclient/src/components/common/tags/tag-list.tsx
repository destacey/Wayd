'use client'

import { TagsOutlined } from '@ant-design/icons'
import { Button, Flex, Tag, Tooltip } from 'antd'
import styles from './tag-list.module.css'

export interface TagListItem {
  id: string
  /** The tag itself — what the chip reads as. */
  label: string
  /**
   * Context shown before the label, dimmed and separated by a pipe. For curated
   * tags this is the axis, so a bare "gold" says what kind of thing it is.
   */
  qualifier?: string
}

export interface TagListProps {
  tags: TagListItem[]
  /**
   * Opens the tag manager. Omit it and the list is read-only — a reader without
   * permission to change tags still sees them.
   */
  onManage?: () => void
  /**
   * How many chips to show before collapsing the rest into a count.
   *
   * A header shares its row with the record name, key and actions, so a heavily
   * tagged record would otherwise push everything else out of shape. Omit it
   * where space is not contended — a details panel, a full-width card.
   */
  maxVisible?: number
  /**
   * Shown when a record carries no tags. Omit it and nothing is drawn — with a
   * manage button present the icon is invitation enough, and a placeholder chip
   * would take header space to say nothing. Worth setting on a read-only view,
   * where an empty row is otherwise ambiguous.
   */
  emptyLabel?: string
}

/**
 * The tags a record carries, as chips.
 *
 * Knows nothing about categories: a caller hands it labels and optional
 * qualifiers, so an area whose tags have no axes uses the same component by
 * passing labels alone.
 *
 * The qualifier is dimmed rather than shown at full weight so the chip's own
 * boundary stays the strongest line — at equal weight, "Platform | ios" reads as
 * two chips rather than one.
 */
const TagList = ({
  tags,
  onManage,
  maxVisible,
  emptyLabel,
}: TagListProps) => {
  const visible = maxVisible ? tags.slice(0, maxVisible) : tags
  const hidden = tags.length - visible.length

  const chip = (tag: TagListItem) => (
    <Tag key={tag.id} className={styles.chip}>
      {tag.qualifier && (
        <span className={styles.qualifier}>{tag.qualifier} | </span>
      )}
      {tag.label}
    </Tag>
  )

  return (
    <Flex align="center" gap={4} wrap className={styles.list}>
      {tags.length > 0 ? (
        <>
          {visible.map(chip)}
          {hidden > 0 && (
            // Titled with what is hidden: a bare "+3" says there is more without
            // saying what, which is the same as saying nothing.
            <Tooltip
              title={tags
                .slice(visible.length)
                .map((tag) =>
                  tag.qualifier ? `${tag.qualifier} | ${tag.label}` : tag.label,
                )
                .join(', ')}
            >
              <Tag className={styles.chip}>+{hidden}</Tag>
            </Tooltip>
          )}
        </>
      ) : (
        emptyLabel && (
          <Tag variant="filled" className={styles.empty}>
            {emptyLabel}
          </Tag>
        )
      )}
      {onManage && (
        <Button
          type="text"
          size="small"
          icon={<TagsOutlined />}
          onClick={onManage}
          aria-label="Manage tags"
          title="Manage tags"
        />
      )}
    </Flex>
  )
}

export default TagList

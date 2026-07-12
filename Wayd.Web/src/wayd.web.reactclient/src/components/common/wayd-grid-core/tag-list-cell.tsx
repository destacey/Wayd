'use client'

import { Tag, Tooltip } from 'antd'

import styles from './tag-list-cell.module.css'

export interface TagListCellProps {
  /** The individual values to render as tags. Blank/empty renders nothing. */
  values: string[] | undefined
}

/**
 * Renders a list of string values as a single, non-wrapping row of antd Tags.
 * The display half of a multi-value ("CSV") grid column — pair with
 * {@link createCsvColumn}. Domain wrappers (e.g. WorkItemTagsCell) delegate here.
 *
 * It behaves like a regular truncating text cell: all tags render in a row and
 * the row's right edge fades out where it overflows the column, signalling
 * "cut off, there's more" without measuring widths or counting a "+N". A
 * tooltip lists every value so the clipped ones stay discoverable.
 */
const TagListCell = ({ values }: TagListCellProps) => {
  const items = values ?? []
  if (items.length === 0) return null

  return (
    <Tooltip title={items.join(', ')}>
      <div className={styles.container}>
        {items.map((v) => (
          <Tag key={v} className={styles.tag}>
            {v}
          </Tag>
        ))}
      </div>
    </Tooltip>
  )
}

export default TagListCell

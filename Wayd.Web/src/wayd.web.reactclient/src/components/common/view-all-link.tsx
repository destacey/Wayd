'use client'

import { RightOutlined } from '@ant-design/icons'
import Link from 'next/link'
import { FC } from 'react'
import styles from './view-all-link.module.css'

interface ViewAllLinkBase {
  /** How many there are in all, shown as "View all 23"; left out when the widget shows them all. */
  count?: number
}

export type ViewAllLinkProps = ViewAllLinkBase &
  (
    | { /** The page the full list lives on. */ href: string; onClick?: never }
    | {
        /** Opens the full list in place, such as a record section or a drawer. */
        onClick: () => void
        href?: never
      }
  )

/**
 * The link from a card or section to the full list it shows part of, placed
 * in the header's top right (a Card's `extra`). One look everywhere: the link
 * colour, since colour is the affordance of a sparse link, and a chevron that
 * says it leads somewhere else. Given `onClick` instead of `href`, it renders a
 * button that looks the same.
 */
const ViewAllLink: FC<ViewAllLinkProps> = ({ href, onClick, count }) => {
  const content = (
    <>
      {count === undefined ? 'View all' : `View all ${count}`}
      <RightOutlined className={styles.chevron} aria-hidden />
    </>
  )

  if (href !== undefined) {
    return (
      <Link href={href} className={styles.viewAll}>
        {content}
      </Link>
    )
  }

  return (
    <button type="button" onClick={onClick} className={styles.viewAll}>
      {content}
    </button>
  )
}

export default ViewAllLink

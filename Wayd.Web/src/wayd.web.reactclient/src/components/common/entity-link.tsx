'use client'

import Link from 'next/link'
import { ComponentProps } from 'react'
import styles from './entity-link.module.css'

export type EntityLinkProps = ComponentProps<typeof Link>

/**
 * Quiet link for entity titles in dense collections (lists, cards, grids)
 * where nearly every item navigates. Renders in the default text color with
 * semibold weight; the link affordance (primary color + underline) appears on
 * hover and keyboard focus.
 *
 * Use the standard blue link for sparse links surrounded by static text
 * (prose, metadata rows, "View all") — color is the affordance there.
 */
const EntityLink = ({ className, ...props }: EntityLinkProps) => (
  <Link
    {...props}
    className={[styles.entityLink, className].filter(Boolean).join(' ')}
  />
)

export default EntityLink

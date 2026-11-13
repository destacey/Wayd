'use client'

import { Card, Skeleton } from 'antd'
import { FC } from 'react'
import styles from './home.module.css'

/** Placeholder cards per column: enough to fill the first screen, not a guess at the real count. */
const MAIN_CARDS = 3
const SIDE_CARDS = 2

const SkeletonCard: FC = () => (
  <Card size="small" className={styles.widgetCard}>
    <Skeleton active paragraph={{ rows: 3 }} />
  </Card>
)

const SkeletonColumn: FC<{ count: number; className: string }> = ({
  count,
  className,
}) => (
  <div className={className}>
    {Array.from({ length: count }, (_, i) => (
      <SkeletonCard key={i} />
    ))}
  </div>
)

/**
 * The page's columns as placeholder cards, shown until the layout is known.
 * Which widgets appear, and where, waits on the saved layout and on what the
 * user is involved in, so the real cards cannot draw their own skeletons yet.
 */
const HomePageSkeleton: FC = () => (
  <div className={styles.columns} aria-busy="true" aria-label="Loading home">
    <SkeletonColumn count={MAIN_CARDS} className={styles.column} />
    <SkeletonColumn
      count={SIDE_CARDS}
      className={`${styles.column} ${styles.side}`}
    />
  </div>
)

export default HomePageSkeleton

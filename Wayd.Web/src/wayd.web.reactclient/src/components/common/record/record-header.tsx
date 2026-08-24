'use client'

import { Flex, Typography } from 'antd'
import Link from 'next/link'
import { ReactNode } from 'react'
import RecordAvatar, { RecordAvatarProps } from '../record-avatar'
import RecordKey from '../record-key'
import styles from './record-layout.module.css'

const { Text, Title } = Typography

export interface RecordHeaderProps {
  /** The record's name — what people call it. */
  name: ReactNode
  /**
   * The record's identifier, shown as its own chip before the name.
   *
   * For teams and teams-of-teams this is the `code`, which is what people say
   * out loud; the numeric key belongs in the record's facts instead.
   */
  recordKey?: string
  /** Leading glyph. A circle for people, a rounded square for everything else. */
  avatar?: RecordAvatarProps
  /** Link back to this record's list, opening the trail beneath the name. */
  parent?: { label: string; href: string }
  /** What kind of page this is, closing the trail. */
  subtitle?: string
  /** Status and other qualifiers, beside the name. */
  tags?: ReactNode
  /** Record-level actions, aligned right. */
  actions?: ReactNode
}

/**
 * The identity bar at the top of a record page.
 *
 * Distinct from `PageTitle`, which heads a list page: this one carries an
 * avatar, a key chip and a breadcrumb trail, and sizes the name to share a
 * tight bar rather than to head a page. Keeping them apart means neither
 * carries branching for the other — `PageTitle` is unchanged from what the
 * legacy pages have always used.
 *
 * Rendered by `RecordLayout`, so pages supply data rather than layout.
 */
const RecordHeader = ({
  name,
  recordKey,
  avatar,
  parent,
  subtitle,
  tags,
  actions,
}: RecordHeaderProps) => (
  <Flex align="center" gap={12} wrap className={styles.headerBar}>
    <Flex align="center" gap={10} style={{ minWidth: 0 }}>
      {avatar && <RecordAvatar {...avatar} />}
      {recordKey && <RecordKey value={recordKey} />}
      <div style={{ minWidth: 0 }}>
        {/* 20px at weight 600 — the name shares this row with the avatar, key,
            status and actions, so a page-sized heading would dominate it.
            Weight is what keeps it above the 16px section heading below. */}
        <Title
          level={4}
          style={{ margin: 0, fontWeight: 600, lineHeight: 1.3 }}
          ellipsis
        >
          {name}
        </Title>
        {(parent || subtitle) && (
          <Flex align="center" gap={6} wrap>
            {parent && (
              <>
                <Link href={parent.href}>
                  <Text type="secondary">{parent.label}</Text>
                </Link>
                {subtitle && (
                  <Text type="secondary" aria-hidden>
                    /
                  </Text>
                )}
              </>
            )}
            {subtitle && <Text type="secondary">{subtitle}</Text>}
          </Flex>
        )}
      </div>
    </Flex>

    {tags}

    {actions && (
      <>
        <div style={{ flexGrow: 1 }} />
        <Flex align="center" gap={8} wrap>
          {actions}
        </Flex>
      </>
    )}
  </Flex>
)

export default RecordHeader

'use client'

import { PicRightOutlined } from '@ant-design/icons'
import { Button, Flex, Typography } from 'antd'
import Link from 'next/link'
import { ReactNode } from 'react'
import RecordAvatar, { RecordAvatarProps } from '../record-avatar'
import RecordKey from '../record-key'
import WaydTooltip from '../wayd-tooltip'
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
  /** Leading glyph. Circle initials for people; no glyph on other records. */
  avatar?: RecordAvatarProps
  /** Link back to this record's list, opening the trail beneath the name. */
  parent?: { label: string; href: string }
  /** What kind of page this is, closing the trail. */
  subtitle?: string
  /** Status and other qualifiers, beside the name. */
  tags?: ReactNode
  /**
   * What this record *is*, in the record's own words — a job title, a
   * methodology. Sits inline after the tags, secondary, so the name stays the
   * thing being read. Distinct from `subtitle`, which names the page.
   */
  descriptor?: ReactNode
  /** Record-level actions, aligned right. */
  actions?: ReactNode
  /**
   * Toggles the record facts panel. Supplied by `RecordLayout`; pages do not
   * set it.
   */
  factsToggle?: { open: boolean; onToggle: (open: boolean) => void }
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
  descriptor,
  actions,
  factsToggle,
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
                {/* Not a `Text type="secondary"` inside the Link — antd's Text
                    sets its own color, which overrode the link styling and
                    left the trail looking like plain muted text. The class
                    starts muted and turns into a link on hover. */}
                <Link href={parent.href} className={styles.parentLink}>
                  {parent.label}
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

    {descriptor && (
      <Text type="secondary" ellipsis>
        {descriptor}
      </Text>
    )}

    {(actions || factsToggle) && (
      <>
        <div style={{ flexGrow: 1 }} />
        <Flex align="center" gap={8} wrap>
          {actions}
          {factsToggle && (
            <WaydTooltip title={factsToggle.open ? 'Hide Details' : 'Show Details'}>
              <Button
                // Same glyph in both states, tinted primary when open — the
                // control lights up rather than turning into another one, the
                // way the section rail marks its active item.
                type={factsToggle.open ? 'default' : 'text'}
                aria-label={factsToggle.open ? 'Hide Details' : 'Show Details'}
                aria-expanded={factsToggle.open}
                icon={
                  <PicRightOutlined
                    style={
                      factsToggle.open
                        ? { color: 'var(--ant-color-primary)' }
                        : undefined
                    }
                  />
                }
                onClick={() => factsToggle.onToggle(!factsToggle.open)}
              />
            </WaydTooltip>
          )}
        </Flex>
      </>
    )}
  </Flex>
)

export default RecordHeader

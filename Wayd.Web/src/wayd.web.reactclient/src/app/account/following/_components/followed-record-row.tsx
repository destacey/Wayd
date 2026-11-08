'use client'

import { MutedOutlined } from '@ant-design/icons'
import { Tooltip } from 'antd'
import Link from 'next/link'
import { LifecycleStatusTag } from '@/src/components/common'
import { FollowedRecordDto } from '@/src/services/wayd-api'
import { formatTimeAgo } from '@/src/utils/time-ago'
import FollowedRecordHealth from './followed-record-health'
import styles from './followed-record-row.module.css'

interface FollowedRecordRowBase {
  record: FollowedRecordDto
  /** What to call the record's kind, such as "Project". */
  typeName: string
}

export type FollowedRecordRowProps = FollowedRecordRowBase &
  (
    | {
        /** Opens the record's detail beside the list, as the Following page does. */
        onSelect: (record: FollowedRecordDto) => void
        selected: boolean
        href?: never
      }
    | {
        /** Goes to the record's own page, as the home widget does. */
        href: string
        onSelect?: never
        selected?: never
      }
  )

/**
 * One followed record: its name, kind, key, parent and when it last changed,
 * with whether it is muted, its health and its status. A button that selects
 * the record on the Following page; a link to the record wherever there is
 * no detail pane to open.
 */
const FollowedRecordRow = ({
  record,
  typeName,
  onSelect,
  selected,
  href,
}: FollowedRecordRowProps) => {
  const content = (
    <>
      <span className={styles.rowBody}>
        <span className={styles.rowName}>{record.name}</span>
        <span className={styles.rowMeta}>
          <span className={styles.rowType}>{typeName}</span>
          {' · '}
          {record.key}
          {record.parentName ? ` · ${record.parentName}` : ''}
          {' · '}
          {record.lastChangedOn
            ? `changed ${formatTimeAgo(record.lastChangedOn)}`
            : 'no changes yet'}
        </span>
      </span>
      <span className={styles.rowTrail}>
        {record.isMuted ? (
          <Tooltip title="Muted: you are told nothing about this">
            <MutedOutlined aria-label="Muted" className={styles.rowMuted} />
          </Tooltip>
        ) : null}
        <FollowedRecordHealth record={record} variant="flag" />
        <LifecycleStatusTag
          status={{
            id: 0,
            name: record.statusName,
            lifecycleCategory: record.lifecycle,
          }}
        />
      </span>
    </>
  )

  if (href !== undefined) {
    return (
      <Link href={href} className={styles.row}>
        {content}
      </Link>
    )
  }

  return (
    <button
      type="button"
      aria-current={selected || undefined}
      className={`${styles.row} ${selected ? styles.rowSelected : ''}`}
      onClick={() => onSelect(record)}
    >
      {content}
    </button>
  )
}

export default FollowedRecordRow

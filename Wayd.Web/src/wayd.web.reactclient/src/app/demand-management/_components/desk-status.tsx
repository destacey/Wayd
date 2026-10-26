'use client'

import { IntakeDeskDto, IntakeDeskWaitingDto } from '@/src/services/wayd-api'
import { formatTimeAgo } from '@/src/utils/time-ago'
import { Alert, Tag } from 'antd'
import { FC } from 'react'

export const deskHref = (key: number) => `/demand-management/desks/${key}`

/**
 * What is waiting on a desk, in words: "3 waiting, the oldest submitted 2 weeks ago".
 */
export const describeWaiting = (waiting: IntakeDeskWaitingDto): string =>
  waiting.count === 0
    ? 'Nothing waiting'
    : `${waiting.count} waiting${
        waiting.oldestSubmittedOn
          ? `, the oldest submitted ${formatTimeAgo(waiting.oldestSubmittedOn)}`
          : ''
      }`

/** Whether nobody works the desk, which is where requests go to be forgotten. */
export const isUnowned = (desk: IntakeDeskDto) => desk.members.length === 0

/**
 * Flags a desk with no members wherever desks are listed.
 */
export const UnownedDeskTag: FC<{ desk: IntakeDeskDto }> = ({ desk }) =>
  isUnowned(desk) ? <Tag color="warning">No members</Tag> : null

/**
 * The warning on a desk nobody works. Says what is waiting there only to those who may know it — the
 * server sends `waiting` to the desk's members and the Demand Management Admin alone.
 */
export const UnownedDeskAlert: FC<{ desk: IntakeDeskDto }> = ({ desk }) =>
  isUnowned(desk) ? (
    <Alert
      type="warning"
      showIcon
      title="Nobody works this desk"
      description={
        desk.waiting
          ? `${describeWaiting(desk.waiting)}. Name its agents so someone looks at what arrives.`
          : 'Requests sent here have nobody to look at them until it has members.'
      }
      style={{ marginBottom: 16 }}
    />
  ) : null

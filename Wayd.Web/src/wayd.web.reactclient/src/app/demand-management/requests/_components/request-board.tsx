'use client'

import { WorkflowStatusTag } from '@/src/components/common/status-workflows'
import {
  IntakeRequestSummaryDto,
  NeedByBasisFamily,
  StatusCategory,
  StatusNavigationDto,
} from '@/src/services/wayd-api'
import { ConfigProvider, Empty, Grid, Select, Typography } from 'antd'
import Link from 'next/link'
import { FC, useState } from 'react'
import { requestHref } from '../../_components/intake-requests-grid'
import { NeedByTag } from '../../_components/need-by'
import { AgeTag } from '../../_components/request-age'
import styles from './request-board.module.css'
import { RequestScope } from './request-filters'

const { Text } = Typography
const { useBreakpoint } = Grid

export interface BoardColumn {
  status: StatusNavigationDto
  requests: IntakeRequestSummaryDto[]
}

const OPEN_CATEGORIES = [StatusCategory.Proposed, StatusCategory.Active]

/** On a phone the stage picker is tapped, so it is a 44px touch target. */
const TOUCH_THEME = { token: { controlHeight: 44 } }

/**
 * The board's columns: every status of the workflow, in its order, holding the requests in it. A stage
 * nobody is in still gets its column. With the open scope the closed stages are left off, since no
 * request in scope can be in them.
 */
export const boardColumns = (
  statuses: StatusNavigationDto[],
  requests: IntakeRequestSummaryDto[],
  scope: RequestScope,
): BoardColumn[] =>
  statuses
    .filter((s) => scope === 'all' || OPEN_CATEGORIES.includes(s.category))
    .map((status) => ({
      status,
      requests: requests.filter((r) => r.status.id === status.id),
    }))

const RequestCard: FC<{ request: IntakeRequestSummaryDto }> = ({ request }) => {
  const fixed = request.needBy?.family === NeedByBasisFamily.FixedForUs
  return (
    <Link
      href={requestHref(request.key)}
      className={`${styles.card} ${fixed ? styles.fixed : ''}`}
    >
      <Text type="secondary" className={styles.number}>
        {request.key}
      </Text>
      <Text strong className={styles.title}>
        {request.title}
      </Text>
      <span>
        <NeedByTag needBy={request.needBy} />
      </span>
      <span className={styles.footer}>
        <Text type="secondary" ellipsis>
          {request.requester.name}
        </Text>
        <AgeTag since={request.statusChangedOn} label="In this status since" />
      </span>
    </Link>
  )
}

const ColumnView: FC<{ column: BoardColumn; showHeader: boolean }> = ({
  column,
  showHeader,
}) => (
  <section
    className={styles.column}
    aria-label={`${column.status.name}, ${column.requests.length}`}
  >
    {showHeader && (
      <header className={styles.columnHeader}>
        <WorkflowStatusTag
          name={column.status.name}
          category={column.status.category}
        />
        <Text type="secondary">{column.requests.length}</Text>
      </header>
    )}
    <div className={styles.cards}>
      {column.requests.length === 0 ? (
        <Text type="secondary" className={styles.empty}>
          Nothing here
        </Text>
      ) : (
        column.requests.map((r) => <RequestCard key={r.id} request={r} />)
      )}
    </div>
  </section>
)

export interface RequestBoardProps {
  columns: BoardColumn[]
}

/**
 * The requests in scope as a board whose columns are the desk workflow's statuses. On a phone one column
 * shows at a time, chosen from a list of the stages with their counts, so nothing scrolls sideways.
 * Cards open the request; moving one goes through the request's own status change, which can ask for a
 * reason or the surviving request, so there is no dragging.
 */
const RequestBoard: FC<RequestBoardProps> = ({ columns }) => {
  const screens = useBreakpoint()
  const [chosenId, setChosenId] = useState<string>()

  if (columns.length === 0)
    return <Empty description="This workflow has no stages to show" />

  if (screens.md === false) {
    const firstWithRequests =
      columns.find((c) => c.requests.length > 0) ?? columns[0]
    const shown =
      columns.find((c) => c.status.id === chosenId) ?? firstWithRequests
    return (
      <div className={styles.single}>
        <ConfigProvider theme={TOUCH_THEME}>
          <Select
            className={styles.stagePicker}
            aria-label="Stage"
            value={shown.status.id}
            onChange={setChosenId}
            options={columns.map((c) => ({
              value: c.status.id,
              label: `${c.status.name} (${c.requests.length})`,
            }))}
          />
        </ConfigProvider>
        <ColumnView column={shown} showHeader={false} />
      </div>
    )
  }

  return (
    <div className={styles.board}>
      {columns.map((c) => (
        <ColumnView key={c.status.id} column={c} showHeader />
      ))}
    </div>
  )
}

export default RequestBoard

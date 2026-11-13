'use client'

import DeliverySignalTag, {
  shownDeliveryState,
} from '@/src/app/demand-management/_components/delivery-signal-tag'
import { WaydList } from '@/src/components/common'
import EntityLink from '@/src/components/common/entity-link'
import { WorkflowStatusTag } from '@/src/components/common/status-workflows'
import {
  IntakeRequestOutcomeDto,
  IntakeRequestOutcomeKind,
  IntakeRequestOutcomesDto,
  IntakeRequestSummaryDto,
  ProjectDeliverableStatusAlias,
  StatusCategory,
} from '@/src/services/wayd-api'
import {
  useGetIntakeRequestOutcomesQuery,
  useGetIntakeRequestsQuery,
} from '@/src/store/features/demand-management/demand-management-api'
import { Flex, Tag, Typography } from 'antd'
import Link from 'next/link'
import { FC } from 'react'
import WidgetCard, { WidgetEmpty, WidgetError } from '../widget-card'
import { rowsToShow } from '../widget-settings'
import { HomeWidgetProps } from '../widget-registry'
import styles from '../home.module.css'

const { Text } = Typography

/** How long a closed request stays on the widget after it closed. */
export const RECENTLY_CLOSED_DAYS = 30

/** The most requests the widget lists before linking to My Requests. */
export const MY_REQUESTS_LIMIT = 8

/**
 * The start of the day {@link RECENTLY_CLOSED_DAYS} days ago, as an ISO
 * instant. Fixed to the day, so every render in it asks the same question and
 * shares one cached answer.
 */
export const recentlyClosedSince = (now: Date = new Date()): string => {
  const since = new Date(now)
  since.setHours(0, 0, 0, 0)
  since.setDate(since.getDate() - RECENTLY_CLOSED_DAYS)
  return since.toISOString()
}

const isOpen = (request: IntakeRequestSummaryDto) =>
  request.status.category === StatusCategory.Proposed ||
  request.status.category === StatusCategory.Active

/** Open requests first, then closed ones; each most recently moved first, then by number. */
export const sortMyRequests = (
  requests: IntakeRequestSummaryDto[],
): IntakeRequestSummaryDto[] =>
  [...requests].sort(
    (a, b) =>
      Number(isOpen(b)) - Number(isOpen(a)) ||
      new Date(b.statusChangedOn).getTime() -
        new Date(a.statusChangedOn).getTime() ||
      b.key - a.key,
  )

/** How far a project scope outcome's mapped deliverables have got, or nothing when it maps none. */
export const deliverableProgress = (
  outcome: IntakeRequestOutcomeDto,
): string | undefined => {
  const onProject = outcome.deliverables.filter((d) => d.isOnProject)
  if (onProject.length === 0) return undefined
  const accepted = onProject.filter(
    (d) => d.statusAlias === ProjectDeliverableStatusAlias.Accepted,
  ).length
  return `${accepted} of ${onProject.length} deliverables accepted`
}

/** One thing the request became, linked to it, with how far it has got. */
const OutcomeLine: FC<{ outcome: IntakeRequestOutcomeDto }> = ({ outcome }) => {
  if (outcome.kind === IntakeRequestOutcomeKind.WorkItem) {
    const workItem = outcome.workItem
    if (!workItem) return <Text type="secondary">A deleted work item</Text>
    return (
      <span className={styles.listItemMeta}>
        <Link
          href={`/work/workspaces/${workItem.workspaceKey}/work-items/${workItem.key}`}
        >
          {workItem.key} · {workItem.title}
        </Link>
        <Text type="secondary">{workItem.status}</Text>
      </span>
    )
  }

  if (!outcome.project) return <Text type="secondary">A deleted project</Text>
  const progress = deliverableProgress(outcome)
  return (
    <span className={styles.listItemMeta}>
      <Link href={`/ppm/projects/${outcome.project.key}`}>
        {outcome.project.key} · {outcome.project.name}
      </Link>
      {outcome.isMet === true ? (
        <Tag color="success">Met</Tag>
      ) : progress ? (
        <Text type="secondary">{progress}</Text>
      ) : null}
    </span>
  )
}

const RequestRow: FC<{
  request: IntakeRequestSummaryDto
  outcomes: IntakeRequestOutcomesDto | undefined
}> = ({ request, outcomes }) => {
  const deliveryState = shownDeliveryState(
    request.status.category,
    outcomes?.deliveryState,
  )
  return (
    <WaydList.Item className={styles.listItem}>
      <div className={styles.listItemBody}>
        <EntityLink href={`/demand-management/requests/${request.key}`}>
          {request.key} · {request.title}
        </EntityLink>
        <span className={styles.listItemMeta}>
          <WorkflowStatusTag
            name={request.status.name}
            category={request.status.category}
          />
          <Text type="secondary">{request.desk.name}</Text>
          {deliveryState && <DeliverySignalTag signal={deliveryState} />}
        </span>
        {outcomes ? (
          <Flex vertical gap={2}>
            {outcomes.outcomes.map((o) => (
              <OutcomeLine key={o.targetId} outcome={o} />
            ))}
          </Flex>
        ) : null}
      </div>
    </WaydList.Item>
  )
}

/**
 * The requests the caller asked for or submitted that are open or closed in
 * the last 30 days, each with its status and what it became.
 */
const MyRequestsWidget: FC<HomeWidgetProps> = ({ title, settings }) => {
  const rows = rowsToShow(settings, MY_REQUESTS_LIMIT)
  const args = { mine: true, closedSince: recentlyClosedSince() }
  const requests = useGetIntakeRequestsQuery(args)
  // Outcomes are extra: without them the requests still show their status.
  const { data: outcomes } = useGetIntakeRequestOutcomesQuery(args)
  const byRequest = new Map((outcomes ?? []).map((o) => [o.requestId, o]))

  const body = () => {
    if (requests.isError)
      return <WidgetError message="Could not load your requests." />
    if (!requests.data || requests.data.length === 0) {
      return (
        <WidgetEmpty
          message="You have no open or recently closed requests. The requests you raise appear here with what they became."
          link={{
            href: '/demand-management/submit',
            label: 'Submit a request',
          }}
        />
      )
    }
    return (
      <WaydList size="small">
        {sortMyRequests(requests.data)
          .slice(0, rows)
          .map((request) => (
            <RequestRow
              key={request.id}
              request={request}
              outcomes={byRequest.get(request.id)}
            />
          ))}
      </WaydList>
    )
  }

  return (
    <WidgetCard
      title={title}
      viewAllHref="/demand-management/my-requests"
      loading={requests.isLoading}
      failed={requests.isError}
      empty={!requests.data || requests.data.length === 0}
      emptySummary="No recent requests"
    >
      {body()}
    </WidgetCard>
  )
}

export default MyRequestsWidget

'use client'

import { WaydList } from '@/src/components/common'
import EntityLink from '@/src/components/common/entity-link'
import { WorkflowStatusTag } from '@/src/components/common/status-workflows'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import { useLinkedEmployee } from '@/src/hooks'
import {
  IntakeDeskDto,
  IntakeDeskRole,
  IntakeRequestSummaryDto,
} from '@/src/services/wayd-api'
import {
  useGetIntakeDesksQuery,
  useGetIntakeRequestsQuery,
} from '@/src/store/features/demand-management/demand-management-api'
import { Flex, Skeleton, Tag, Typography } from 'antd'
import Link from 'next/link'
import { FC } from 'react'
import AttentionTile from '../attention-tile'
import { useHomeContext } from '../use-home-context'
import WidgetCard, { WidgetEmpty, WidgetError } from '../widget-card'
import { HomeWidgetProps } from '../widget-registry'
import styles from '../home.module.css'

const { Text } = Typography

/** The most requests each list shows before linking to the rest. */
export const DESK_QUEUE_LIMIT = 5

const REQUESTS = '/demand-management/requests'
/** The Requests page filtered to the reader's desks and to no assignee, by its URL filters. */
export const UNASSIGNED_ON_MY_DESKS_HREF = `${REQUESTS}?desk=mine&assignee=unassigned`
/** The Requests page filtered to requests assigned to the reader. */
export const ASSIGNED_TO_ME_HREF = `${REQUESTS}?assignee=me`

/** One of the reader's desks with their role on it. */
export interface MyDesk {
  desk: IntakeDeskDto
  role: IntakeDeskRole
}

/**
 * The desks the employee is an Admin or Agent on, by name. Membership, not the
 * Demand Management Admin's reach over every desk: the queue is the desks the
 * reader works.
 */
export const myDesks = (
  desks: IntakeDeskDto[],
  employeeId: string | null,
): MyDesk[] =>
  desks
    .flatMap((desk) => {
      const member = desk.members.find((m) => m.employee.id === employeeId)
      return member ? [{ desk, role: member.role }] : []
    })
    .sort((a, b) => caseInsensitiveCompare(a.desk.name, b.desk.name))

/** The open requests no one is assigned to, across the reader's desks. */
export const countUnassigned = (desks: MyDesk[]): number =>
  desks.reduce((sum, { desk }) => sum + (desk.waiting?.unassignedCount ?? 0), 0)

/**
 * The desk list and the reader's place on each. Skipped when the home context
 * says they are on no desk.
 */
const useMyDesks = () => {
  const { employeeId } = useLinkedEmployee()
  const { context } = useHomeContext()
  const query = useGetIntakeDesksQuery(undefined, {
    skip: context?.intake.deskCount === 0,
  })
  return {
    ...query,
    data: query.data ? myDesks(query.data, employeeId) : undefined,
  }
}

/** The open requests assigned to the reader; the widget and its tile share the request. */
const useAssignedToMe = () => {
  const { context } = useHomeContext()
  return useGetIntakeRequestsQuery(
    { assignedToMe: true, openOnly: true },
    { skip: context?.intake.deskCount === 0 },
  )
}

const roleLabel: Record<IntakeDeskRole, string> = {
  [IntakeDeskRole.Admin]: 'Desk Admin',
  [IntakeDeskRole.Agent]: 'Agent',
}

/** A desk's open count and what awaits triage on it; a count the desk does not use is left out. */
const DeskCounts: FC<{ desk: IntakeDeskDto }> = ({ desk }) => {
  const waiting = desk.waiting
  if (!waiting) return null
  // The server sends null, not nothing, for a count the desk does not use.
  const counts: [string, number | null | undefined][] = [
    ['open', waiting.count],
    ['unassigned', waiting.unassignedCount],
    ['unscored', waiting.unscoredCount],
    ['unestimated', waiting.unestimatedCount],
  ]
  return (
    <span className={styles.listItemMeta}>
      {counts.flatMap(([label, n]) =>
        typeof n === 'number' ? (
          <Text
            key={label}
            type={label !== 'open' && n > 0 ? 'warning' : 'secondary'}
          >
            {n} {label}
          </Text>
        ) : (
          []
        ),
      )}
    </span>
  )
}

const RequestRows: FC<{ requests: IntakeRequestSummaryDto[] }> = ({
  requests,
}) => (
  <WaydList size="small">
    {requests.slice(0, DESK_QUEUE_LIMIT).map((request) => (
      <WaydList.Item key={request.id} className={styles.listItem}>
        <div className={styles.listItemBody}>
          <EntityLink href={`${REQUESTS}/${request.key}`}>
            {request.key} · {request.title}
          </EntityLink>
          <span className={styles.listItemMeta}>
            <WorkflowStatusTag
              name={request.status.name}
              category={request.status.category}
            />
            <Text type="secondary">{request.desk.name}</Text>
          </span>
        </div>
      </WaydList.Item>
    ))}
  </WaydList>
)

/** A titled list of requests, with a link to the rest when there are more than it shows. */
const RequestSection: FC<{
  heading: string
  requests: IntakeRequestSummaryDto[] | undefined
  isLoading: boolean
  isError: boolean
  empty: string
  moreHref?: string
}> = ({ heading, requests, isLoading, isError, empty, moreHref }) => (
  <Flex vertical gap={4}>
    <Text strong>{heading}</Text>
    {isError ? (
      <WidgetError message={`Could not load ${heading.toLowerCase()}.`} />
    ) : isLoading ? (
      <Skeleton active title={false} paragraph={{ rows: 2 }} />
    ) : !requests || requests.length === 0 ? (
      <WidgetEmpty message={empty} />
    ) : (
      <>
        <RequestRows requests={requests} />
        {moreHref && requests.length > DESK_QUEUE_LIMIT ? (
          <Link href={moreHref}>All {requests.length}</Link>
        ) : null}
      </>
    )}
  </Flex>
)

/**
 * What waits on the desks the caller works: each desk's open requests and what
 * still needs triage, the requests assigned to them, and those they take part in.
 */
const DeskQueueWidget: FC<HomeWidgetProps> = ({ title }) => {
  const desks = useMyDesks()
  const assigned = useAssignedToMe()
  const { context } = useHomeContext()
  const participating = useGetIntakeRequestsQuery(
    { participating: true, openOnly: true },
    { skip: context?.intake.deskCount === 0 },
  )

  const body = () => {
    if (desks.isError)
      return <WidgetError message="Could not load your desks." />
    if (!desks.data || desks.data.length === 0) {
      return (
        <WidgetEmpty message="You are not on any intake desk. Requests waiting on your desks appear here once a Desk Admin adds you as an Agent." />
      )
    }
    return (
      <Flex vertical gap="middle">
        <WaydList size="small">
          {desks.data.map(({ desk, role }) => (
            <WaydList.Item key={desk.id} className={styles.listItem}>
              <div className={styles.listItemBody}>
                <Flex align="center" gap={8} wrap>
                  <EntityLink href={`/demand-management/desks/${desk.key}`}>
                    {desk.name}
                  </EntityLink>
                  <Tag>{roleLabel[role]}</Tag>
                </Flex>
                <DeskCounts desk={desk} />
              </div>
            </WaydList.Item>
          ))}
        </WaydList>
        <RequestSection
          heading="Assigned to me"
          requests={assigned.data}
          isLoading={assigned.isLoading}
          isError={assigned.isError}
          empty="No open request is assigned to you."
          moreHref={ASSIGNED_TO_ME_HREF}
        />
        <RequestSection
          heading="Participating"
          requests={participating.data}
          isLoading={participating.isLoading}
          isError={participating.isError}
          empty="You take part in no open request."
        />
      </Flex>
    )
  }

  return (
    <WidgetCard
      title={title}
      viewAllHref={`${REQUESTS}?desk=mine`}
      loading={desks.isLoading}
    >
      {body()}
    </WidgetCard>
  )
}

/** Open requests on the caller's desks that no one is assigned to. */
export const UnassignedRequestsTile: FC = () => {
  const { data, isLoading, isError } = useMyDesks()
  return (
    <AttentionTile
      title="Unassigned requests"
      value={isError ? undefined : countUnassigned(data ?? [])}
      tooltip="Open intake requests on the desks you work that no one is assigned to."
      href={UNASSIGNED_ON_MY_DESKS_HREF}
      alert="warning"
      loading={isLoading}
      context="on your desks"
    />
  )
}

/** Open requests assigned to the caller. */
export const RequestsAssignedToMeTile: FC = () => {
  const { data, isLoading, isError } = useAssignedToMe()
  return (
    <AttentionTile
      title="Requests assigned"
      value={isError ? undefined : (data?.length ?? 0)}
      tooltip="Open intake requests assigned to you."
      href={ASSIGNED_TO_ME_HREF}
      alert="warning"
      loading={isLoading}
      context="to you"
    />
  )
}

export default DeskQueueWidget

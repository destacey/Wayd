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
import {
  pickedIdsOf,
  pickedOf,
  rowsToShow,
  WidgetMultiSelectSetting,
  WidgetPickOptionsSource,
  WidgetPickSetting,
  WidgetSetting,
  WidgetSettingValues,
} from '../widget-settings'
import { HomeTileProps, HomeWidgetProps } from '../widget-registry'
import styles from '../home.module.css'

const { Text } = Typography

/** The most requests each list shows before linking to the rest. */
export const DESK_QUEUE_LIMIT = 5

const REQUESTS = '/demand-management/requests'
/** The Requests page filtered to the reader's desks and to no assignee, by its URL filters. */
export const UNASSIGNED_ON_MY_DESKS_HREF = `${REQUESTS}?desk=mine&assignee=unassigned`
/** The Requests page filtered to requests assigned to the reader. */
export const ASSIGNED_TO_ME_HREF = `${REQUESTS}?assignee=me`

/** The key of the desk queue's choice of desks. */
export const DESKS_SETTING_KEY = 'desks'
/** The key of the desk queue's choice of sections. */
export const SECTIONS_SETTING_KEY = 'sections'

/** A part of the desk queue the user can drop. */
export type DeskQueueSection = 'desks' | 'assigned' | 'participating'

const ALL_SECTIONS: DeskQueueSection[] = ['desks', 'assigned', 'participating']

/**
 * The Requests page filtered by `query`, to the desks the queue covers: the
 * reader's own when it is not narrowed, otherwise the desks chosen, by id.
 */
const requestsHref = (query: string, deskIds: string[]) => {
  const params = new URLSearchParams(query)
  if (deskIds.length > 0) {
    params.delete('desk')
    deskIds.forEach((id) => params.append('desk', id))
  }
  const search = params.toString()
  return search ? `${REQUESTS}?${search}` : REQUESTS
}

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

/** What the desk queue covers: the desks chosen and the sections shown. */
interface DeskQueueScope {
  /** The ids of the desks chosen that the reader still works; empty for every desk. */
  deskIds: string[]
  sections: DeskQueueSection[]
}

/**
 * What the queue's settings narrow it to, the desk ids checked against the
 * desks the reader works, once those have loaded.
 */
const scopeOf = (
  settings: WidgetSettingValues | undefined,
  desks: MyDesk[] | undefined,
): DeskQueueScope => ({
  deskIds: pickedIdsOf(
    settings,
    DESKS_SETTING_KEY,
    desks?.map(({ desk }) => ({ value: desk.id })),
  ),
  sections: pickedOf(
    settings,
    SECTIONS_SETTING_KEY,
    ALL_SECTIONS,
  ) as DeskQueueSection[],
})

/** The entries on the desks the scope covers. */
const onDesks = <T,>(
  items: T[] | undefined,
  deskIds: string[],
  deskOf: (item: T) => string,
): T[] | undefined =>
  items && deskIds.length > 0
    ? items.filter((item) => deskIds.includes(deskOf(item)))
    : items

/** Every desk the reader works, for the queue's choice of desks. */
const DeskOptions: WidgetPickOptionsSource = ({ children }) => {
  const { data, isLoading, isError } = useMyDesks()
  return children({
    options: (data ?? []).map(({ desk }) => ({
      value: desk.id,
      label: desk.name,
    })),
    isLoading,
    isError,
  })
}

/** Which of the reader's desks the queue shows. */
const desksPickSetting: WidgetPickSetting = {
  kind: 'pick',
  key: DESKS_SETTING_KEY,
  label: 'Desks',
  noun: 'desks',
  Options: DeskOptions,
}

/** Which parts of the queue show. */
const sectionsSetting: WidgetMultiSelectSetting = {
  kind: 'multiSelect',
  key: SECTIONS_SETTING_KEY,
  label: 'Sections',
  options: [
    { value: 'desks', label: 'My desks' },
    { value: 'assigned', label: 'Assigned to me' },
    { value: 'participating', label: 'Participating' },
  ],
  default: ALL_SECTIONS,
}

/** The desk queue's own settings, before the ones every list widget shares. */
export const deskQueueSettings: WidgetSetting[] = [
  desksPickSetting,
  sectionsSetting,
]

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

const RequestRows: FC<{
  requests: IntakeRequestSummaryDto[]
  rows: number
}> = ({ requests, rows }) => (
  <WaydList size="small">
    {requests.slice(0, rows).map((request) => (
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
  /** The most requests the list shows. */
  rows: number
  moreHref?: string
}> = ({ heading, requests, isLoading, isError, empty, rows, moreHref }) => (
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
        <RequestRows requests={requests} rows={rows} />
        {moreHref && requests.length > rows ? (
          <Link href={moreHref}>All {requests.length}</Link>
        ) : null}
      </>
    )}
  </Flex>
)

/** Whether a request list loaded with nothing in it; a failed one is not empty. */
const isListEmpty = ({
  data,
  isLoading,
  isError,
}: {
  data?: unknown[]
  isLoading: boolean
  isError: boolean
}) => !isLoading && !isError && (!data || data.length === 0)

/**
 * What waits on the desks the caller works: each desk's open requests and what
 * still needs triage, the requests assigned to them, and those they take part in.
 */
const DeskQueueWidget: FC<HomeWidgetProps> = ({ title, settings }) => {
  const rows = rowsToShow(settings, DESK_QUEUE_LIMIT)
  const desks = useMyDesks()
  const { deskIds, sections } = scopeOf(settings, desks.data)
  const shows = (section: DeskQueueSection) => sections.includes(section)
  const assigned = useAssignedToMe()
  const { context } = useHomeContext()
  const participating = useGetIntakeRequestsQuery(
    { participating: true, openOnly: true },
    { skip: context?.intake.deskCount === 0 || !shows('participating') },
  )
  const shownDesks = onDesks(desks.data, deskIds, ({ desk }) => desk.id)
  const assignedRequests = onDesks(assigned.data, deskIds, (r) => r.desk.id)
  const participatingRequests = onDesks(
    participating.data,
    deskIds,
    (r) => r.desk.id,
  )

  const hasNoDesks = !desks.data || desks.data.length === 0
  const requestLists = [
    ...(shows('assigned') ? [{ ...assigned, data: assignedRequests }] : []),
    ...(shows('participating')
      ? [{ ...participating, data: participatingRequests }]
      : []),
  ]

  const body = () => {
    if (desks.isError)
      return <WidgetError message="Could not load your desks." />
    if (!shownDesks || shownDesks.length === 0) {
      return (
        <WidgetEmpty message="You are not on any intake desk. Requests waiting on your desks appear here once a Desk Admin adds you as an Agent." />
      )
    }
    return (
      <Flex vertical gap="middle">
        {shows('desks') ? (
          <WaydList size="small">
            {shownDesks.map(({ desk, role }) => (
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
        ) : null}
        {shows('assigned') ? (
          <RequestSection
            heading="Assigned to me"
            requests={assignedRequests}
            isLoading={assigned.isLoading}
            isError={assigned.isError}
            rows={rows}
            empty="No open request is assigned to you."
            moreHref={requestsHref('assignee=me', deskIds)}
          />
        ) : null}
        {shows('participating') ? (
          <RequestSection
            heading="Participating"
            requests={participatingRequests}
            isLoading={participating.isLoading}
            isError={participating.isError}
            rows={rows}
            empty="You take part in no open request."
          />
        ) : null}
      </Flex>
    )
  }

  return (
    <WidgetCard
      title={title}
      viewAllHref={requestsHref('desk=mine', deskIds)}
      // Waits for the request lists too: whether the widget is empty depends on them.
      loading={
        desks.isLoading ||
        (!hasNoDesks && requestLists.some((list) => list.isLoading))
      }
      failed={desks.isError}
      // With no request list shown, the desks themselves are what it shows.
      empty={
        hasNoDesks ||
        (requestLists.length > 0 && requestLists.every(isListEmpty))
      }
      emptySummary={hasNoDesks ? 'Not on any desk' : 'No requests for you'}
    >
      {body()}
    </WidgetCard>
  )
}

/**
 * Open requests on the caller's desks that no one is assigned to: only on the
 * desks the queue shows, and none while it leaves out its desks.
 */
export const UnassignedRequestsTile: FC<HomeTileProps> = ({ settings }) => {
  const { data, isLoading, isError } = useMyDesks()
  const { deskIds, sections } = scopeOf(settings, data)
  const desks = onDesks(data, deskIds, ({ desk }) => desk.id) ?? []
  return (
    <AttentionTile
      title="Unassigned requests"
      value={
        isError
          ? undefined
          : sections.includes('desks')
            ? countUnassigned(desks)
            : 0
      }
      tooltip="Open intake requests on the desks you work that no one is assigned to."
      href={requestsHref('desk=mine&assignee=unassigned', deskIds)}
      alert="warning"
      loading={isLoading}
      context="on your desks"
    />
  )
}

/**
 * Open requests assigned to the caller: only on the desks the queue shows, and
 * none while it leaves out its Assigned to me list.
 */
export const RequestsAssignedToMeTile: FC<HomeTileProps> = ({ settings }) => {
  const desks = useMyDesks()
  const { data, isLoading, isError } = useAssignedToMe()
  const { deskIds, sections } = scopeOf(settings, desks.data)
  const requests = onDesks(data, deskIds, (r) => r.desk.id) ?? []
  return (
    <AttentionTile
      title="Requests assigned"
      value={
        isError
          ? undefined
          : sections.includes('assigned')
            ? requests.length
            : 0
      }
      tooltip="Open intake requests assigned to you."
      href={requestsHref('assignee=me', deskIds)}
      alert="warning"
      loading={isLoading || (deskIds.length > 0 && desks.isLoading)}
      context="to you"
    />
  )
}

export default DeskQueueWidget

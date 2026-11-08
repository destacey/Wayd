'use client'

import { ProjectDeliverableDto, StatusCategory } from '@/src/services/wayd-api'
import { useGetProjectDeliverablesQuery } from '@/src/store/features/ppm/project-deliverables-api'
import {
  compareCalendarDates,
  formatCalendarDate,
} from '@/src/utils/calendar-date'
import ViewAllLink from '@/src/components/common/view-all-link'
import { Card, Flex, Progress, Tag, Typography } from 'antd'
import dayjs from 'dayjs'
import DeliverableProgress from '@/src/app/ppm/projects/_components/deliverable-progress'
import { isOverdue } from './deliverable-status'

const { Text } = Typography

/** Deliverables listed on the card; the rest are a click away. */
const NEXT_DUE_COUNT = 3

export interface ProjectDeliverablesSummaryProps {
  projectId: string
  projectKey: string
  style?: React.CSSProperties
}

/** Earliest target first; undated deliverables last, in their agreed order. */
const byTargetDate = (a: ProjectDeliverableDto, b: ProjectDeliverableDto) => {
  if (a.targetDate && b.targetDate)
    return compareCalendarDates(a.targetDate, b.targetDate) || a.order - b.order
  if (a.targetDate) return -1
  if (b.targetDate) return 1
  return a.order - b.order
}

/** Latest acceptance first; ties and undated acceptances in their agreed order. */
const byMostRecentlyAccepted = (
  a: ProjectDeliverableDto,
  b: ProjectDeliverableDto,
) =>
  (b.acceptedOn?.getTime() ?? 0) - (a.acceptedOn?.getTime() ?? 0) ||
  a.order - b.order

/** Rows after the first are ruled off from the one above. */
const rowStyle = (index: number): React.CSSProperties => ({
  padding: '6px 0',
  borderTop:
    index > 0 ? '1px solid var(--ant-color-border-secondary)' : undefined,
})

/**
 * Where the project stands against what it committed to: how many deliverables
 * are accepted, and the next few still open, soonest due first, each with how
 * far its linked plan work has got when any is linked. Once none is open, the
 * most recently accepted are listed instead, so the card still names what was
 * delivered. Descoped deliverables leave the count, since they no longer stand
 * between the project and done. Renders nothing for a project with no
 * deliverables in scope.
 */
const ProjectDeliverablesSummary = ({
  projectId,
  projectKey,
  style,
}: ProjectDeliverablesSummaryProps) => {
  const { data } = useGetProjectDeliverablesQuery(projectId)

  const inScope = (data ?? []).filter(
    (d) => d.status.category !== StatusCategory.Removed,
  )
  if (inScope.length === 0) return null

  const accepted = inScope.filter(
    (d) => d.status.category === StatusCategory.Done,
  ).length
  const nextDue = inScope.filter((d) => d.isOpen).sort(byTargetDate)
  const recentlyAccepted =
    nextDue.length === 0
      ? inScope
          .filter((d) => d.status.category === StatusCategory.Done)
          .sort(byMostRecentlyAccepted)
      : []

  return (
    <Card
      size="small"
      title="Deliverables"
      style={style}
      extra={
        <ViewAllLink href={`/ppm/projects/${projectKey}?section=deliverables`} />
      }
    >
      <Flex gap="large" align="center" wrap>
        <Flex vertical align="center" gap={4} style={{ minWidth: 110 }}>
          <Progress
            type="circle"
            size={84}
            percent={Math.round((accepted / inScope.length) * 100)}
            format={() => `${accepted} of ${inScope.length}`}
            aria-label={`${accepted} of ${inScope.length} deliverables accepted`}
          />
          <Text type="secondary">accepted</Text>
        </Flex>
        <Flex vertical style={{ flex: '1 1 240px', minWidth: 0 }}>
          {recentlyAccepted.slice(0, NEXT_DUE_COUNT).map((d, i) => (
            <Flex
              key={d.id}
              justify="space-between"
              align="center"
              gap="small"
              wrap
              style={rowStyle(i)}
            >
              <Text ellipsis style={{ flex: '1 1 160px', minWidth: 0 }}>
                {d.name}
              </Text>
              <Flex gap="small" align="center">
                {d.acceptedOn && (
                  <Text type="secondary" style={{ whiteSpace: 'nowrap' }}>
                    {dayjs(d.acceptedOn).format('MMM D, YYYY')}
                  </Text>
                )}
                <Tag color="success" style={{ marginInlineEnd: 0 }}>
                  {d.status.name}
                </Tag>
              </Flex>
            </Flex>
          ))}
          {nextDue.slice(0, NEXT_DUE_COUNT).map((d, i) => (
            <Flex
              key={d.id}
              justify="space-between"
              align="center"
              gap="small"
              wrap
              style={rowStyle(i)}
            >
              <Text ellipsis style={{ flex: '1 1 160px', minWidth: 0 }}>
                {d.name}
              </Text>
              <Flex gap="small" align="center">
                {d.progress !== null && d.progress !== undefined && (
                  <DeliverableProgress progress={d.progress} width={90} />
                )}
                <Text type="secondary" style={{ whiteSpace: 'nowrap' }}>
                  {d.targetDate ? formatCalendarDate(d.targetDate) : 'No date'}
                </Text>
                {isOverdue(d) && (
                  <Tag color="warning" style={{ marginInlineEnd: 0 }}>
                    Overdue
                  </Tag>
                )}
              </Flex>
            </Flex>
          ))}
        </Flex>
      </Flex>
    </Card>
  )
}

export default ProjectDeliverablesSummary

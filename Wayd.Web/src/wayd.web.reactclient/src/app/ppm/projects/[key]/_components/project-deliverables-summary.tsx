'use client'

import { WorkflowStatusTag } from '@/src/components/common/status-workflows'
import { ProjectDeliverableDto, StatusCategory } from '@/src/services/wayd-api'
import { useGetProjectDeliverablesQuery } from '@/src/store/features/ppm/project-deliverables-api'
import { formatCalendarDate } from '@/src/utils/calendar-date'
import { Flex, Progress, Tag, Typography } from 'antd'
import Link from 'next/link'
import { isOverdue } from './deliverable-status'
import Block from './overview-block'

const { Text } = Typography

export interface ProjectDeliverablesSummaryProps {
  projectId: string
  projectKey: string
}

/**
 * Where the project stands against what it committed to: how many deliverables
 * are accepted, and which are still open. Descoped deliverables leave the
 * count, since they no longer stand between the project and done.
 *
 * Deliberately a list rather than a track or timeline, so it does not read as a
 * second stage strip beside the one above it. Renders nothing for a project
 * with no deliverables.
 */
const ProjectDeliverablesSummary = ({
  projectId,
  projectKey,
}: ProjectDeliverablesSummaryProps) => {
  const { data } = useGetProjectDeliverablesQuery(projectId)

  const inScope = (data ?? []).filter(
    (d) => d.status.category !== StatusCategory.Removed,
  )
  if (inScope.length === 0) return null

  const accepted = inScope.filter(
    (d) => d.status.category === StatusCategory.Done,
  ).length
  const open: ProjectDeliverableDto[] = inScope.filter((d) => d.isOpen)

  return (
    <Block
      title="Deliverables"
      extra={
        <Link href={`/ppm/projects/${projectKey}?section=deliverables`}>
          View all
        </Link>
      }
    >
      <Flex align="center" gap="small">
        <Progress
          percent={Math.round((accepted / inScope.length) * 100)}
          showInfo={false}
          style={{ flex: 1, margin: 0 }}
          aria-label={`${accepted} of ${inScope.length} deliverables accepted`}
        />
        <Text type="secondary" style={{ whiteSpace: 'nowrap' }}>
          {accepted} of {inScope.length} accepted
        </Text>
      </Flex>
      {open.length > 0 && (
        <Flex vertical>
          {open.map((d) => (
            <Flex
              key={d.id}
              gap="small"
              align="center"
              wrap
              style={{
                padding: '6px 0',
                borderTop: '1px solid var(--ant-color-border-secondary)',
              }}
            >
              <Text style={{ flex: '1 1 220px' }}>{d.name}</Text>
              <WorkflowStatusTag
                name={d.status.name}
                category={d.status.category}
              />
              <Text type="secondary" style={{ minWidth: 110 }}>
                {formatCalendarDate(d.targetDate)}
              </Text>
              {isOverdue(d) && <Tag color="warning">Overdue</Tag>}
            </Flex>
          ))}
        </Flex>
      )}
    </Block>
  )
}

export default ProjectDeliverablesSummary

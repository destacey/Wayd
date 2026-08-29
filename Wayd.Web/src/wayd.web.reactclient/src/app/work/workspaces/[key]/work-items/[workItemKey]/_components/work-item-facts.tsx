'use client'

import { LabeledContent } from '@/src/components/common/content'
import { SprintLink } from '@/src/components/common/planning'
import { RecordFactsGroup } from '@/src/components/common/record'
import { WorkTypeTier } from '@/src/components/types'
import { WorkItemDetailsDto } from '@/src/services/wayd-api'
import { Divider, Flex, Space, Tag } from 'antd'
import dayjs from 'dayjs'
import Link from 'next/link'

export interface WorkItemFactsProps {
  workItem: WorkItemDetailsDto
}

const TIMESTAMP_FORMAT = 'MMM D, YYYY @ h:mm A'

/**
 * A work item's stable facts, for the details panel.
 *
 * Its classification and estimate, then the people and records it hangs off,
 * then its audit trail. The status *category* is deliberately absent — the
 * status tag in the identity bar already carries it, and repeating it here
 * read as two different fields.
 */
const WorkItemFacts = ({ workItem }: WorkItemFactsProps) => {
  const teamLink =
    workItem.team?.type === 'Team'
      ? `/organizations/teams/${workItem.team?.key}`
      : `/organizations/team-of-teams/${workItem.team?.key}`

  return (
    <>
      <Flex vertical gap={10}>
        <LabeledContent label="Type">{workItem.type.name}</LabeledContent>

        {workItem.priority != null && (
          <LabeledContent label="Priority">{workItem.priority}</LabeledContent>
        )}

        {workItem.storyPoints != null && (
          <LabeledContent label="Story Points">
            {workItem.storyPoints}
          </LabeledContent>
        )}

        {workItem.tags && workItem.tags.length > 0 && (
          <LabeledContent label="Tags">
            <Space wrap size={[4, 4]}>
              {workItem.tags.map((tag) => (
                <Tag key={tag}>{tag}</Tag>
              ))}
            </Space>
          </LabeledContent>
        )}
      </Flex>

      <Divider size="small" style={{ margin: 0 }} />

      <RecordFactsGroup label="People">
        <LabeledContent label="Assigned To">
          {workItem.assignedTo ? (
            <Link href={`/organizations/employees/${workItem.assignedTo.key}`}>
              {workItem.assignedTo.name}
            </Link>
          ) : (
            'Unassigned'
          )}
        </LabeledContent>

        <LabeledContent label="Team">
          {workItem.team ? (
            <Link href={teamLink}>{workItem.team.name}</Link>
          ) : (
            'No team'
          )}
        </LabeledContent>
      </RecordFactsGroup>

      <Divider size="small" style={{ margin: 0 }} />

      <RecordFactsGroup label="Relationships">
        <LabeledContent label="Parent">
          {workItem.parent ? (
            <Link
              href={`/work/workspaces/${workItem.parent.workspaceKey}/work-items/${workItem.parent.key}`}
            >
              {workItem.parent.key} - {workItem.parent.title}
            </Link>
          ) : (
            'No parent'
          )}
        </LabeledContent>

        {workItem.type.tier.id === WorkTypeTier.Requirement && (
          <LabeledContent label="Sprint">
            {workItem.sprint ? (
              <SprintLink sprint={workItem.sprint} />
            ) : (
              'Backlog'
            )}
          </LabeledContent>
        )}

        <LabeledContent label="Project">
          {workItem.project ? (
            <Link href={`/ppm/projects/${workItem.project.key}`}>
              {workItem.project.name}
            </Link>
          ) : (
            'No project'
          )}
        </LabeledContent>
      </RecordFactsGroup>

      <Divider size="small" style={{ margin: 0 }} />

      <RecordFactsGroup label="History">
        <LabeledContent label="Created">
          {dayjs(workItem.created).format(TIMESTAMP_FORMAT)}
          {workItem.createdBy && (
            <Link href={`/organizations/employees/${workItem.createdBy.key}`}>
              {workItem.createdBy.name}
            </Link>
          )}
        </LabeledContent>

        <LabeledContent label="Updated">
          {dayjs(workItem.lastModified).format(TIMESTAMP_FORMAT)}
          {workItem.lastModifiedBy && (
            <Link
              href={`/organizations/employees/${workItem.lastModifiedBy.key}`}
            >
              {workItem.lastModifiedBy.name}
            </Link>
          )}
        </LabeledContent>
      </RecordFactsGroup>
    </>
  )
}

export default WorkItemFacts

'use client'

import { WorkItemDetailsDto } from '@/src/services/wayd-api'
import { SprintLink } from '@/src/components/common/planning'
import { Descriptions, Space, Tag } from 'antd'
import dayjs from 'dayjs'
import Link from 'next/link'
import WorkItemSteps from './work-item-steps'
import { WorkTypeTier } from '@/src/components/types'

const { Item } = Descriptions

export interface WorkItemDetailsProps {
  workItem: WorkItemDetailsDto
}

const WorkItemDetails = ({ workItem }: WorkItemDetailsProps) => {
  if (!workItem) return null

  const teamLink =
    workItem.team?.type === 'Team'
      ? `/organizations/teams/${workItem.team?.key}`
      : `/organizations/team-of-teams/${workItem.team?.key}`

  return (
    <>
      <Descriptions>
        <Item label="Key">{workItem.key}</Item>
        <Item label="Type">{workItem.type.name}</Item>
        <Item label="Status">{workItem.status}</Item>
        <Item label="Status Category">{workItem.statusCategory.name}</Item>
        <Item label="Priority">{workItem.priority}</Item>
        <Item label="Story Points">{workItem.storyPoints}</Item>
        <Item label="Team">
          <Link href={teamLink}>{workItem.team?.name}</Link>
        </Item>
        {workItem.type.tier.id === WorkTypeTier.Requirement && (
          <Item label="Sprint">
            {workItem.sprint ? (
              <SprintLink sprint={workItem.sprint} />
            ) : (
              'Backlog'
            )}
          </Item>
        )}
        <Item label="Parent">
          {workItem.parent ? (
            <Link
              href={`/work/workspaces/${workItem.parent.workspaceKey}/work-items/${workItem.parent.key}`}
            >
              {workItem.parent.key} - {workItem.parent.title}
            </Link>
          ) : (
            'No Parent'
          )}
        </Item>
        <Item label="Project">
          {workItem.project ? (
            <Link href={`/ppm/projects/${workItem.project.key}`}>
              {workItem.project.name}
            </Link>
          ) : (
            'No Project'
          )}
        </Item>
        <Item label="Assigned To">
          {workItem.assignedTo ? (
            <Link href={`/organizations/employees/${workItem.assignedTo.key}`}>
              {workItem.assignedTo.name}
            </Link>
          ) : (
            'Unassigned'
          )}
        </Item>
        <Item label="Created By">
          {workItem.createdBy ? (
            <Link href={`/organizations/employees/${workItem.createdBy.key}`}>
              {workItem.createdBy.name}
            </Link>
          ) : (
            'Unknown'
          )}
        </Item>
        <Item label="Created">
          {dayjs(workItem.created).format('MMM D, YYYY @ h:mm A')}
        </Item>
        <Item label="Last Modified By">
          {workItem.lastModifiedBy ? (
            <Link
              href={`/organizations/employees/${workItem.lastModifiedBy.key}`}
            >
              {workItem.lastModifiedBy.name}
            </Link>
          ) : (
            'Unknown'
          )}
        </Item>
        <Item label="Updated">
          {dayjs(workItem.lastModified).format('MMM D, YYYY @ h:mm A')}
        </Item>
        {workItem.tags && workItem.tags.length > 0 && (
          <Item label="Tags">
            <Space wrap size={[4, 4]}>
              {workItem.tags.map((tag) => (
                <Tag key={tag}>{tag}</Tag>
              ))}
            </Space>
          </Item>
        )}
      </Descriptions>
      <br />
      <WorkItemSteps workItem={workItem} />
    </>
  )
}

export default WorkItemDetails

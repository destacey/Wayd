'use client'

import { WorkspaceDto } from '@/src/services/wayd-api'
import { Descriptions } from 'antd'
import Link from 'next/link'

const { Item } = Descriptions

export interface WorkspaceDetailsProps {
  workspace: WorkspaceDto
}

const WorkspaceDetails = ({ workspace }: WorkspaceDetailsProps) => {
  return (
    <>
      <Descriptions>
        <Item label="Key">{workspace.key}</Item>
        <Item label="Work Process">
          <Link
            href={`/settings/work-management/work-processes/${workspace.workProcess.id}`}
          >
            {workspace.workProcess.name}
          </Link>
        </Item>
        <Item label="Ownership">{workspace.ownership.name}</Item>
        <Item label="Is Active?">{workspace.isActive.toString()}</Item>
      </Descriptions>
      <Descriptions>
        <Item label="Description">{workspace.description}</Item>
      </Descriptions>
    </>
  )
}

export default WorkspaceDetails

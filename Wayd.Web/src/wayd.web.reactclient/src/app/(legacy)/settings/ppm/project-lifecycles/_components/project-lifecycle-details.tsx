'use client'

import { ProjectLifecycleDetailsDto } from '@/src/services/wayd-api'
import { Descriptions } from 'antd'

const { Item } = Descriptions

interface ProjectLifecycleDetailsProps {
  lifecycle: ProjectLifecycleDetailsDto | undefined
}

const ProjectLifecycleDetails: React.FC<ProjectLifecycleDetailsProps> = ({
  lifecycle,
}: ProjectLifecycleDetailsProps) => {
  if (!lifecycle) return null

  return (
    <Descriptions column={1} size="small">
      <Item label="State">{lifecycle.state?.name}</Item>
      <Item label="Description">{lifecycle.description}</Item>
    </Descriptions>
  )
}

export default ProjectLifecycleDetails

'use client'

import { Tag } from 'antd'
import { FC, memo } from 'react'
import { StatusCategory } from '@/src/services/wayd-api'

export interface WorkflowStatusTagProps {
  name: string
  category: StatusCategory
}

// Deliberately not reusing WorkStatusTag: work item DTOs send the category as a numeric id
// (StatusCategoryId), where workflow statuses send the StatusCategory name. Casting between the two
// would compile while silently mapping the wrong colour.
const getTagColor = (category: StatusCategory): string => {
  switch (category) {
    case StatusCategory.Proposed:
      return 'default'
    case StatusCategory.Active:
      return 'processing'
    case StatusCategory.Done:
      return 'success'
    case StatusCategory.Removed:
      return 'warning'
    default:
      return 'default'
  }
}

const WorkflowStatusTag: FC<WorkflowStatusTagProps> = ({ name, category }) => (
  <Tag color={getTagColor(category)}>{name}</Tag>
)

export default memo(WorkflowStatusTag)

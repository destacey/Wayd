'use client'

import { Tag } from 'antd'
import { FC, memo } from 'react'
import { StatusCategory } from '@/src/services/wayd-api'

export interface WorkflowStatusTagProps {
  name: string
  category: StatusCategory
}

// Deliberately not reusing WorkStatusTag. Its WorkStatusCategory is a numeric enum from the work
// module; this one is a string enum from the workflow engine. They share four names today by
// coincidence, and casting between them would compile while silently mapping the wrong colour the
// moment either side gains a category.
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

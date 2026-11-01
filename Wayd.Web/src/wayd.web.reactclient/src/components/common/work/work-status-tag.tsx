'use client'

import { Tag } from 'antd'
import { StatusCategoryId } from '../../types'
import { FC, memo } from 'react'

export interface WorkStatusTagProps {
  status: string
  category: StatusCategoryId
}

const getTagColor = (category: StatusCategoryId): string => {
  switch (category) {
    case StatusCategoryId.Proposed:
      return 'default'
    case StatusCategoryId.Active:
      return 'processing'
    case StatusCategoryId.Done:
      return 'success'
    case StatusCategoryId.Removed:
      return 'warning'
    default:
      return 'default'
  }
}

const WorkStatusTag: FC<WorkStatusTagProps> = ({ status, category }) => {
  const color = getTagColor(category)
  return <Tag color={color}>{status}</Tag>
}

export default memo(WorkStatusTag)

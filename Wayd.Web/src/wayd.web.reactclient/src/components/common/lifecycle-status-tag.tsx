'use client'

import { LifecycleNavigationDto } from '@/src/services/wayd-api'
import { FC, memo } from 'react'
import { Tag } from 'antd'
import { LifecycleCategory } from '../types'
import { getLifecycleCategoryTagColor } from '@/src/utils'

export interface LifecycleStatusTagProps {
  status: LifecycleNavigationDto
}

const LifecycleStatusTag: FC<LifecycleStatusTagProps> = ({ status }) => {
  const category =
    LifecycleCategory[status.lifecycleCategory as keyof typeof LifecycleCategory]
  const color = getLifecycleCategoryTagColor(category)

  return <Tag color={color}>{status.name}</Tag>
}

export default memo(LifecycleStatusTag)

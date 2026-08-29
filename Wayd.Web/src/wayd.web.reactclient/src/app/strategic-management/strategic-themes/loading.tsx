'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function StrategicThemesLoading() {
  return (
    <div className="page-gutters">
      <PageTitle title="Strategic Themes" />
      <Skeleton active />
    </div>
  )
}

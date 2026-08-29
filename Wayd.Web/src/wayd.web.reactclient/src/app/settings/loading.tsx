'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function SettingsLoading() {
  return (
    <div className="page-gutters">
      <PageTitle title="Settings" />
      <Skeleton active />
    </div>
  )
}

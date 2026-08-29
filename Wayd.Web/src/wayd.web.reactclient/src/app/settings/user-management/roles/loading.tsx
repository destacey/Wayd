'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function RolesLoading() {
  return (
    <div className="page-gutters">
      <PageTitle title="Roles" />
      <Skeleton active />
    </div>
  )
}

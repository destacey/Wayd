'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function UsersLoading() {
  return (
    <div className="page-gutters">
      <PageTitle title="Users" />
      <Skeleton active />
    </div>
  )
}

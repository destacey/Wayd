'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function ImportsLoading() {
  return (
    <div className="page-gutters">
      <PageTitle title="Imports" />
      <Skeleton active />
    </div>
  )
}

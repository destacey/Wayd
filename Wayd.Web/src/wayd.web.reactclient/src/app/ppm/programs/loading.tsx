'use client'

import { PageTitle } from '@/src/components/common'
import { Skeleton } from 'antd'

export default function ProgramsLoading() {
  return (
    <div className="page-gutters">
      <PageTitle title="Programs" />
      <Skeleton active />
    </div>
  )
}

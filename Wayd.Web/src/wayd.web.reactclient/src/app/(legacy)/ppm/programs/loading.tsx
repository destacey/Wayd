'use client'

import { PageTitle } from '@/src/components/common'
import { Skeleton } from 'antd'

export default function ProgramsLoading() {
  return (
    <>
      <PageTitle title="Programs" />
      <Skeleton active />
    </>
  )
}

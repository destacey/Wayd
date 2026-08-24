'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function ProjectDetailsLoading() {
  return (
    <>
      <PageTitle title="Project" />
      <Skeleton active />
    </>
  )
}

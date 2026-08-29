'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function RolesLoading() {
  return (
    <>
      <PageTitle title="Roles" />
      <Skeleton active />
    </>
  )
}

'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function UserDetailsLoading() {
  return (
    <>
      <PageTitle title="User Details" />
      <Skeleton active />
    </>
  )
}

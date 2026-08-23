'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function RoleDetailsLoading() {
  return (
    <>
      <PageTitle title="Role Details" />
      <Skeleton active />
    </>
  )
}

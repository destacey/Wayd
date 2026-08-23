'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function TeamOfTeamDetailsLoading() {
  return (
    <>
      <PageTitle title="Team of Teams Details" />
      <Skeleton active />
    </>
  )
}

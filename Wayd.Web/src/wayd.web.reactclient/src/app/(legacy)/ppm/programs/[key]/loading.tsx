'use client'

import { PageTitle } from '@/src/components/common'
import { Card, Skeleton } from 'antd'

const tabs = [
  {
    key: 'details',
    label: 'Details',
  },
  {
    key: 'projects',
    label: 'Projects',
  },
]

export default function ProgramDetailsLoading() {
  return (
    <>
      <PageTitle title="" subtitle="Program Details" />
      <Card style={{ width: '100%' }} tabList={tabs} activeTabKey="details">
        <Skeleton active />
      </Card>
    </>
  )
}

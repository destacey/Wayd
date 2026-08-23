'use client'

import PageTitle from '@/src/components/common/page-title'
import { authorizePage } from '@/src/components/hoc'
import { notFound } from 'next/navigation'
import { use, useEffect, useState } from 'react'
import { Card } from 'antd'
import BasicBreadcrumb from '@/src/components/common/basic-breadcrumb'
import WorkProcessDetailsLoading from './loading'
import { PageActions } from '@/src/components/common'
import useAuth from '@/src/components/contexts/auth'
import ChangeWorkProcessIsActiveForm from '../_components/change-work-process-isactive-form'
import { useGetWorkProcessQuery } from '@/src/store/features/work-management/work-process-api'
import { ItemType } from 'antd/es/menu/interface'
import { WorkProcessDetails } from '../_components'

enum WorkProcessDetailsTabs {
  Details = 'details',
}

const tabs = [
  {
    key: 'details',
    tab: 'Details',
  },
]

const WorkProcessDetailsPage = (props: {
  params: Promise<{ key: string }>
}) => {
  const { key } = use(props.params)
  const workProcessKey = Number(key)

  const [activeTab, setActiveTab] = useState(WorkProcessDetailsTabs.Details)
  const [
    openChangeWorkProcessIsActiveForm,
    setOpenChangeWorkProcessIsActiveForm,
  ] = useState<boolean>(false)

  const { hasPermissionClaim } = useAuth()
  const canUpdateWorkProcess = hasPermissionClaim(
    'Permissions.WorkProcesses.Update',
  )

  const {
    data: workProcessData,
    isLoading,
    error,
    refetch,
  } = useGetWorkProcessQuery(workProcessKey.toString())

  const renderTabContent = () => {
    switch (activeTab) {
      case WorkProcessDetailsTabs.Details:
        return <WorkProcessDetails workProcess={workProcessData!} />
      default:
        return null
    }
  }

  const onTabChange = (tabKey: string) => {
    setActiveTab(tabKey as WorkProcessDetailsTabs)
  }

  useEffect(() => {
    error && console.error(error)
  }, [error])

  const actionsMenuItems = (() => {
    if (workProcessData?.isActive === undefined) return [] as ItemType[]

    const items = [] as ItemType[]
    if (canUpdateWorkProcess) {
      const activationManagementLabel = workProcessData?.isActive
        ? 'Deactivate'
        : 'Activate'
      items.push({
        key: 'activate-menu-item',
        label: activationManagementLabel,
        onClick: () => setOpenChangeWorkProcessIsActiveForm(true),
      })
    }
    return items
  })()

  if (isLoading) {
    return <WorkProcessDetailsLoading />
  }

  if (!workProcessData) {
    return notFound()
  }

  const onChangeWorkProcessIsActiveFormClosed = (wasSaved: boolean) => {
    if (wasSaved) {
      refetch()
    }
    setOpenChangeWorkProcessIsActiveForm(false)
  }

  return (
    <>
      <BasicBreadcrumb
        items={[
          { title: 'Settings' },
          { title: 'Work Management' },
          { title: 'Work Processes', href: './' },
          { title: 'Details' },
        ]}
      />
      <PageTitle
        title={`${workProcessData?.key} - ${workProcessData?.name}`}
        subtitle="Work Process Details"
        actions={<PageActions actionItems={actionsMenuItems} />}
      />
      <Card
        style={{ width: '100%' }}
        tabList={tabs}
        activeTabKey={activeTab}
        onTabChange={onTabChange}
      >
        {renderTabContent()}
      </Card>
      {openChangeWorkProcessIsActiveForm && (
        <ChangeWorkProcessIsActiveForm
          workProcessId={workProcessData?.id}
          workProcessName={workProcessData?.name}
          isActive={!!workProcessData?.isActive}
          onFormSave={() => onChangeWorkProcessIsActiveFormClosed(true)}
          onFormCancel={() => onChangeWorkProcessIsActiveFormClosed(false)}
        />
      )}
    </>
  )
}

const WorkProcessDetailsPageWithAuthorization = authorizePage(
  WorkProcessDetailsPage,
  'Permission',
  'Permissions.WorkProcesses.View',
)

export default WorkProcessDetailsPageWithAuthorization

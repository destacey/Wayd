'use client'

import { PageActions, PageTitle } from '@/src/components/common'
import BasicBreadcrumb from '@/src/components/common/basic-breadcrumb'
import useAuth from '@/src/components/contexts/auth'
import { authorizePage, requireFeatureFlag } from '@/src/components/hoc'
import { Card, MenuProps } from 'antd'
import { use, useEffect, useState } from 'react'
import EstimationScaleDetailsLoading from './loading'
import { notFound, useRouter } from 'next/navigation'
import {
  DeleteEstimationScaleForm,
  EditEstimationScaleForm,
  EstimationScaleDetails,
} from '../_components'
import {
  useGetEstimationScaleQuery,
  useSetEstimationScaleActiveStatusMutation,
} from '@/src/store/features/planning/estimation-scales-api'
import { ItemType } from 'antd/es/menu/interface'
import { useMessage } from '@/src/components/contexts/messaging'
import { isApiError } from '@/src/utils'

enum EstimationScaleTabs {
  Details = 'details',
}

const tabs = [
  {
    key: EstimationScaleTabs.Details,
    tab: 'Details',
  },
]

enum MenuActions {
  Edit = 'Edit',
  Delete = 'Delete',
}

const EstimationScaleDetailsPage = (props: {
  params: Promise<{ id: number }>
}) => {
  const { id } = use(props.params)

  const [activeTab, setActiveTab] = useState(EstimationScaleTabs.Details)
  const [openEditForm, setOpenEditForm] = useState<boolean>(false)
  const [openDeleteForm, setOpenDeleteForm] = useState<boolean>(false)

  const messageApi = useMessage()
  const router = useRouter()

  const {
    data: scaleData,
    isLoading,
    error,
    refetch,
  } = useGetEstimationScaleQuery(id)
  const [setActiveStatus] = useSetEstimationScaleActiveStatusMutation()

  const { hasPermissionClaim } = useAuth()
  const canUpdateEstimationScales = hasPermissionClaim(
    'Permissions.EstimationScales.Update',
  )
  const canDeleteEstimationScales = hasPermissionClaim(
    'Permissions.EstimationScales.Delete',
  )

  const renderTabContent = () => {
    switch (activeTab) {
      case EstimationScaleTabs.Details:
        return <EstimationScaleDetails estimationScale={scaleData!} />
      default:
        return null
    }
  }

  const onTabChange = (tabKey: string) => {
    setActiveTab(tabKey as EstimationScaleTabs)
  }

  const handleToggleActive = async () => {
    if (!scaleData) return
    try {
      const response = await setActiveStatus({
        id: scaleData.id,
        isActive: !scaleData.isActive,
      })
      if (response.error) {
        throw response.error
      }
      messageApi.success(
        `Estimation scale ${scaleData.isActive ? 'deactivated' : 'activated'} successfully.`,
      )
    } catch (error) {
      messageApi.error(
        'An error occurred while updating the estimation scale status.',
      )
      console.error(error)
    }
  }

  const actionsMenuItems: MenuProps['items'] = (() => {
    const items: ItemType[] = []
    if (canUpdateEstimationScales) {
      items.push({
        key: 'edit',
        label: MenuActions.Edit,
        onClick: () => setOpenEditForm(true),
      })
      items.push({
        key: 'toggle-active',
        label: scaleData?.isActive ? 'Deactivate' : 'Activate',
        onClick: handleToggleActive,
      })
    }
    if (canDeleteEstimationScales) {
      items.push({
        key: 'delete',
        label: MenuActions.Delete,
        onClick: () => setOpenDeleteForm(true),
      })
    }

    return items
  })()

  useEffect(() => {
    if (error) {
      messageApi.error(
        (isApiError(error) ? error.detail : undefined) ??
          'An error occurred while loading estimation scale details',
      )
      console.error(error)
    }
  }, [error, messageApi])

  const onEditFormClosed = (wasSaved: boolean) => {
    setOpenEditForm(false)
    if (wasSaved) {
      refetch()
    }
  }

  const onDeleteFormClosed = (wasDeleted: boolean) => {
    setOpenDeleteForm(false)
    if (wasDeleted) {
      router.push('/settings/planning/estimation-scales')
    }
  }

  if (isLoading) {
    return <EstimationScaleDetailsLoading />
  }

  if (!scaleData) {
    return notFound()
  }

  return (
    <>
      <BasicBreadcrumb
        items={[
          { title: 'Settings' },
          { title: 'Planning' },
          { title: 'Estimation Scales', href: './' },
          { title: 'Details' },
        ]}
      />
      <PageTitle
        title={`${scaleData?.name}`}
        subtitle="Estimation Scale Details"
        actions={
          actionsMenuItems.length > 0 ? (
            <PageActions actionItems={actionsMenuItems} />
          ) : undefined
        }
      />
      <Card
        style={{ width: '100%' }}
        tabList={tabs}
        activeTabKey={activeTab}
        onTabChange={onTabChange}
      >
        {renderTabContent()}
      </Card>

      {openEditForm && (
        <EditEstimationScaleForm
          estimationScaleId={scaleData?.id}
          onFormComplete={() => onEditFormClosed(true)}
          onFormCancel={() => onEditFormClosed(false)}
        />
      )}
      {openDeleteForm && (
        <DeleteEstimationScaleForm
          estimationScale={scaleData}
          onFormComplete={() => onDeleteFormClosed(true)}
          onFormCancel={() => onDeleteFormClosed(false)}
        />
      )}
    </>
  )
}

const EstimationScaleDetailsPageWithAuthorization = requireFeatureFlag(
  authorizePage(EstimationScaleDetailsPage, 'Permission', 'Permissions.EstimationScales.View'),
  'planning-poker',
)

export default EstimationScaleDetailsPageWithAuthorization

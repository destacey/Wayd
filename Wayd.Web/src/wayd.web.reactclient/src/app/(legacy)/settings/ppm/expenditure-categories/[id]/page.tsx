'use client'

import { PageActions, PageTitle } from '@/src/components/common'
import BasicBreadcrumb from '@/src/components/common/basic-breadcrumb'
import useAuth from '@/src/components/contexts/auth'
import { authorizePage } from '@/src/components/hoc'
import { Card, MenuProps } from 'antd'
import { use, useEffect, useState } from 'react'
import ExpenditureCategorieDetailsLoading from './loading'
import { notFound, useRouter } from 'next/navigation'
import {
  DeleteExpenditureCategoryForm,
  EditExpenditureCategoryForm,
  ExpenditureCategoryDetails,
} from '../_components'
import { useGetExpenditureCategoryQuery } from '@/src/store/features/ppm/expenditure-categories-api'
import { ItemType } from 'antd/es/menu/interface'
import ChangeExpenditureCategoryStateForm, {
  ExpenditureCategoryStateAction,
} from '../_components/change-expenditure-category-state-form'
import { useMessage } from '@/src/components/contexts/messaging'
import { isApiError } from '@/src/utils'

enum ExpenditureCategoryTabs {
  Details = 'details',
}

const tabs = [
  {
    key: ExpenditureCategoryTabs.Details,
    tab: 'Details',
  },
]

enum MenuActions {
  Edit = 'Edit',
  Delete = 'Delete',
  Activate = 'Activate',
  Archive = 'Archive',
}

const ExpenditureCategoryDetailsPage = (props: {
  params: Promise<{ id: number }>
}) => {
  const { id } = use(props.params)

  const [activeTab, setActiveTab] = useState(ExpenditureCategoryTabs.Details)
  const [openEditExpenditureCategoryForm, setOpenEditExpenditureCategoryForm] =
    useState<boolean>(false)
  const [
    openActivateExpenditureCategoryForm,
    setOpenActivateExpenditureCategoryForm,
  ] = useState<boolean>(false)
  const [
    openArchiveExpenditureCategoryForm,
    setOpenArchiveExpenditureCategoryForm,
  ] = useState<boolean>(false)
  const [
    openDeleteExpenditureCategoryForm,
    setOpenDeleteExpenditureCategoryForm,
  ] = useState<boolean>(false)

  const messageApi = useMessage()

  const router = useRouter()

  const {
    data: categoryData,
    isLoading,
    error,
    refetch,
  } = useGetExpenditureCategoryQuery(id)

  const { hasPermissionClaim } = useAuth()
  const canUpdateExpenditureCategories = hasPermissionClaim(
    'Permissions.ExpenditureCategories.Update',
  )
  const canDeleteExpenditureCategories = hasPermissionClaim(
    'Permissions.ExpenditureCategories.Delete',
  )

  const renderTabContent = () => {
    switch (activeTab) {
      case ExpenditureCategoryTabs.Details:
        return <ExpenditureCategoryDetails expenditureCategory={categoryData!} />
      default:
        return null
    }
  }

  const onTabChange = (tabKey: string) => {
    setActiveTab(tabKey as ExpenditureCategoryTabs)
  }

  const actionsMenuItems: MenuProps['items'] = (() => {
    const currentState = categoryData?.state.name
    const availableActions =
      currentState === 'Proposed'
        ? [MenuActions.Delete, MenuActions.Activate]
        : currentState === 'Active'
          ? [MenuActions.Archive]
          : []

    const items: ItemType[] = []
    if (canUpdateExpenditureCategories) {
      items.push({
        key: 'edit',
        label: MenuActions.Edit,
        onClick: () => setOpenEditExpenditureCategoryForm(true),
      })
    }
    if (
      canDeleteExpenditureCategories &&
      availableActions.includes(MenuActions.Delete)
    ) {
      items.push({
        key: 'delete',
        label: MenuActions.Delete,
        onClick: () => setOpenDeleteExpenditureCategoryForm(true),
      })
    }

    if (
      (canUpdateExpenditureCategories &&
        availableActions.includes(MenuActions.Activate)) ||
      availableActions.includes(MenuActions.Archive)
    ) {
      items.push({
        key: 'manage-divider',
        type: 'divider',
      })
    }

    if (
      canUpdateExpenditureCategories &&
      availableActions.includes(MenuActions.Activate)
    ) {
      items.push({
        key: 'activate',
        label: MenuActions.Activate,
        onClick: () => setOpenActivateExpenditureCategoryForm(true),
      })
    }

    if (
      canUpdateExpenditureCategories &&
      availableActions.includes(MenuActions.Archive)
    ) {
      items.push({
        key: 'archive',
        label: MenuActions.Archive,
        onClick: () => setOpenArchiveExpenditureCategoryForm(true),
      })
    }

    return items
  })()

  useEffect(() => {
    if (error) {
      messageApi.error(
        (isApiError(error) ? error.detail : undefined) ??
          'An error occurred while loading expenditure category details',
      )
      console.error(error)
    }
  }, [error, messageApi])

  const onEditExpenditureCategoryFormClosed = (wasSaved: boolean) => {
    setOpenEditExpenditureCategoryForm(false)
    if (wasSaved) {
      refetch()
    }
  }

  const onActivateExpenditureCategoryFormClosed = (wasSaved: boolean) => {
    setOpenActivateExpenditureCategoryForm(false)
    if (wasSaved) {
      refetch()
    }
  }

  const onArchiveExpenditureCategoryFormClosed = (wasSaved: boolean) => {
    setOpenArchiveExpenditureCategoryForm(false)
    if (wasSaved) {
      refetch()
    }
  }

  const onDeleteExpenditureCategoryFormClosed = (wasDeleted: boolean) => {
    setOpenDeleteExpenditureCategoryForm(false)
    if (wasDeleted) {
      router.push('/settings/ppm/expenditure-categories')
    }
  }

  if (isLoading) {
    return <ExpenditureCategorieDetailsLoading />
  }

  if (!categoryData) {
    return notFound()
  }

  return (
    <>
      <BasicBreadcrumb
        items={[
          { title: 'Settings' },
          { title: 'PPM' },
          { title: 'Expenditure Categories', href: './' },
          { title: 'Details' },
        ]}
      />
      <PageTitle
        title={`${categoryData?.id} - ${categoryData?.name}`}
        subtitle="Expenditure Category Details"
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

      {openEditExpenditureCategoryForm && (
        <EditExpenditureCategoryForm
          expenditureCategoryId={categoryData?.id}
          onFormComplete={() => onEditExpenditureCategoryFormClosed(true)}
          onFormCancel={() => onEditExpenditureCategoryFormClosed(false)}
        />
      )}
      {openActivateExpenditureCategoryForm && (
        <ChangeExpenditureCategoryStateForm
          expenditureCategory={categoryData}
          stateAction={ExpenditureCategoryStateAction.Activate}
          onFormComplete={() => onActivateExpenditureCategoryFormClosed(true)}
          onFormCancel={() => onActivateExpenditureCategoryFormClosed(false)}
        />
      )}
      {openArchiveExpenditureCategoryForm && (
        <ChangeExpenditureCategoryStateForm
          expenditureCategory={categoryData}
          stateAction={ExpenditureCategoryStateAction.Archive}
          onFormComplete={() => onArchiveExpenditureCategoryFormClosed(true)}
          onFormCancel={() => onArchiveExpenditureCategoryFormClosed(false)}
        />
      )}
      {openDeleteExpenditureCategoryForm && (
        <DeleteExpenditureCategoryForm
          expenditureCategory={categoryData}
          onFormComplete={() => onDeleteExpenditureCategoryFormClosed(true)}
          onFormCancel={() => onDeleteExpenditureCategoryFormClosed(false)}
        />
      )}
    </>
  )
}

const ExpenditureCategoryDetailsPageWithAuthorization = authorizePage(
  ExpenditureCategoryDetailsPage,
  'Permission',
  'Permissions.ExpenditureCategories.View',
)

export default ExpenditureCategoryDetailsPageWithAuthorization

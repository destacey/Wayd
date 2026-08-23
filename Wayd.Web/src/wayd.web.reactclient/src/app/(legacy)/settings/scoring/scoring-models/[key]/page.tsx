'use client'

import { PageActions, PageTitle } from '@/src/components/common'
import BasicBreadcrumb from '@/src/components/common/basic-breadcrumb'
import useAuth from '@/src/components/contexts/auth'
import { authorizePage } from '@/src/components/hoc'
import { Card, MenuProps } from 'antd'
import { use, useEffect, useState } from 'react'
import ScoringModelDetailsLoading from './loading'
import { notFound, useRouter } from 'next/navigation'
import ScoringModelDetails from '../_components/scoring-model-details'
import { useGetScoringModelQuery } from '@/src/store/features/scoring/scoring-models-api'
import { ItemType } from 'antd/es/menu/interface'
import { useMessage } from '@/src/components/contexts/messaging'
import EditScoringModelForm from '../_components/edit-scoring-model-form'
import DeleteScoringModelForm from '../_components/delete-scoring-model-form'
import ChangeScoringModelStateForm, {
  ScoringModelStateAction,
} from '../_components/change-scoring-model-state-form'
import ScoringModelCriteriaList from '../_components/scoring-model-criteria-list'
import ScoringScalesList from '../_components/scoring-scales-list'
import ScoringModelOutputsList from '../_components/scoring-model-outputs-list'
import ScoringModelTestPanel from '../_components/scoring-model-test-panel'
import { useDocumentTitle } from '@/src/hooks/use-document-title'
import { isApiError } from '@/src/utils'

enum ScoringModelTabs {
  Details = 'details',
  Criteria = 'criteria',
  RatingScale = 'rating-scale',
  Outputs = 'outputs',
  Test = 'test',
}

const tabs = [
  {
    key: ScoringModelTabs.Details,
    tab: 'Details',
  },
  {
    key: ScoringModelTabs.Criteria,
    tab: 'Criteria',
  },
  {
    key: ScoringModelTabs.RatingScale,
    tab: 'Rating Scales',
  },
  {
    key: ScoringModelTabs.Outputs,
    tab: 'Outputs',
  },
  {
    key: ScoringModelTabs.Test,
    tab: 'Test',
  },
]

enum MenuActions {
  Edit = 'Edit',
  Delete = 'Delete',
  Activate = 'Activate',
  Archive = 'Archive',
}

const ScoringModelDetailsPage = (props: {
  params: Promise<{ key: number }>
}) => {
  const { key } = use(props.params)

  const [activeTab, setActiveTab] = useState(ScoringModelTabs.Details)
  const [openEditForm, setOpenEditForm] = useState<boolean>(false)
  const [openActivateForm, setOpenActivateForm] = useState<boolean>(false)
  const [openArchiveForm, setOpenArchiveForm] = useState<boolean>(false)
  const [openDeleteForm, setOpenDeleteForm] = useState<boolean>(false)

  const messageApi = useMessage()

  const router = useRouter()

  const {
    data: scoringModelData,
    isLoading,
    error,
    refetch,
  } = useGetScoringModelQuery(key.toString())

  const { hasPermissionClaim } = useAuth()
  const canUpdate = hasPermissionClaim('Permissions.ScoringModels.Update')
  const canDelete = hasPermissionClaim('Permissions.ScoringModels.Delete')

  const isProposed = scoringModelData?.state?.name === 'Proposed'
  const canManage = canUpdate && isProposed

  const title = scoringModelData
    ? `${scoringModelData.name} - Scoring Model Details`
    : 'Scoring Model Details'
  useDocumentTitle(title)

  const renderTabContent = () => {
    switch (activeTab) {
      case ScoringModelTabs.Details:
        return <ScoringModelDetails scoringModel={scoringModelData!} />
      case ScoringModelTabs.Criteria:
        return (
          <ScoringModelCriteriaList
            scoringModel={scoringModelData!}
            canManage={canManage}
            loadData={refetch}
          />
        )
      case ScoringModelTabs.RatingScale:
        return (
          <ScoringScalesList
            scoringModel={scoringModelData!}
            canManage={canManage}
            loadData={refetch}
          />
        )
      case ScoringModelTabs.Outputs:
        return (
          <ScoringModelOutputsList
            scoringModel={scoringModelData!}
            canManage={canManage}
            loadData={refetch}
          />
        )
      case ScoringModelTabs.Test:
        return <ScoringModelTestPanel scoringModel={scoringModelData!} />
      default:
        return null
    }
  }

  const onTabChange = (tabKey: string) => {
    setActiveTab(tabKey as ScoringModelTabs)
  }

  const actionsMenuItems: MenuProps['items'] = (() => {
    const currentState = scoringModelData?.state?.name
    const availableActions =
      currentState === 'Proposed'
        ? [MenuActions.Delete, MenuActions.Activate]
        : currentState === 'Active'
          ? [MenuActions.Archive]
          : []

    const items: ItemType[] = []
    if (canUpdate && currentState === 'Proposed') {
      items.push({
        key: 'edit',
        label: MenuActions.Edit,
        onClick: () => setOpenEditForm(true),
      })
    }
    if (canDelete && availableActions.includes(MenuActions.Delete)) {
      items.push({
        key: 'delete',
        label: MenuActions.Delete,
        onClick: () => setOpenDeleteForm(true),
      })
    }

    const hasStateActions =
      (canUpdate && availableActions.includes(MenuActions.Activate)) ||
      (canUpdate && availableActions.includes(MenuActions.Archive))

    if (hasStateActions && items.length > 0) {
      items.push({
        key: 'manage-divider',
        type: 'divider',
      })
    }

    if (canUpdate && availableActions.includes(MenuActions.Activate)) {
      items.push({
        key: 'activate',
        label: MenuActions.Activate,
        onClick: () => setOpenActivateForm(true),
      })
    }

    if (canUpdate && availableActions.includes(MenuActions.Archive)) {
      items.push({
        key: 'archive',
        label: MenuActions.Archive,
        onClick: () => setOpenArchiveForm(true),
      })
    }

    return items
  })()

  useEffect(() => {
    if (error) {
      messageApi.error(
        (isApiError(error) ? error.detail : undefined) ??
          'An error occurred while loading scoring model details',
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

  const onActivateFormClosed = (wasSaved: boolean) => {
    setOpenActivateForm(false)
    if (wasSaved) {
      refetch()
    }
  }

  const onArchiveFormClosed = (wasSaved: boolean) => {
    setOpenArchiveForm(false)
    if (wasSaved) {
      refetch()
    }
  }

  const onDeleteFormClosed = (wasDeleted: boolean) => {
    setOpenDeleteForm(false)
    if (wasDeleted) {
      router.push('/settings/scoring/scoring-models')
    }
  }

  if (isLoading) {
    return <ScoringModelDetailsLoading />
  }

  if (!scoringModelData) {
    return notFound()
  }

  return (
    <>
      <BasicBreadcrumb
        items={[
          { title: 'Settings' },
          { title: 'Scoring' },
          { title: 'Scoring Models', href: './' },
          { title: 'Details' },
        ]}
      />
      <PageTitle
        title={`${scoringModelData?.key} - ${scoringModelData?.name}`}
        subtitle="Scoring Model Details"
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

      {openEditForm && (
        <EditScoringModelForm
          scoringModelId={scoringModelData?.id}
          onFormComplete={() => onEditFormClosed(true)}
          onFormCancel={() => onEditFormClosed(false)}
        />
      )}
      {openActivateForm && (
        <ChangeScoringModelStateForm
          scoringModel={scoringModelData}
          stateAction={ScoringModelStateAction.Activate}
          onFormComplete={() => onActivateFormClosed(true)}
          onFormCancel={() => onActivateFormClosed(false)}
        />
      )}
      {openArchiveForm && (
        <ChangeScoringModelStateForm
          scoringModel={scoringModelData}
          stateAction={ScoringModelStateAction.Archive}
          onFormComplete={() => onArchiveFormClosed(true)}
          onFormCancel={() => onArchiveFormClosed(false)}
        />
      )}
      {openDeleteForm && (
        <DeleteScoringModelForm
          scoringModel={scoringModelData}
          onFormComplete={() => onDeleteFormClosed(true)}
          onFormCancel={() => onDeleteFormClosed(false)}
        />
      )}
    </>
  )
}

const ScoringModelDetailsPageWithAuthorization = authorizePage(
  ScoringModelDetailsPage,
  'Permission',
  'Permissions.ScoringModels.View',
)

export default ScoringModelDetailsPageWithAuthorization


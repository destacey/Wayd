'use client'

import PageTitle from '@/src/components/common/page-title'
import { Card, MenuProps } from 'antd'
import { use, useState } from 'react'
import { useDocumentTitle } from '@/src/hooks/use-document-title'
import useAuth from '@/src/components/contexts/auth'
import { useLinkedEmployee } from '@/src/hooks'
import { ItemType } from 'antd/es/menu/interface'
import { authorizePage } from '@/src/components/hoc'
import { notFound, useRouter } from 'next/navigation'
import PlanningIntervalObjectiveDetailsLoading from './loading'
import PiObjectiveHealthCheckTag from '@/src/app/planning/planning-intervals/_components/pi-objective-health-check-tag'
import Link from 'next/link'
import { PageActions } from '@/src/components/common'
import { useGetPlanningIntervalObjectiveQuery } from '@/src/store/features/planning/planning-interval-api'
import { PlanningIntervalObjectiveDetails } from './_components'
import {
  CreatePlanningIntervalObjectiveHealthCheckForm,
  DeletePlanningIntervalObjectiveForm,
  EditPlanningIntervalObjectiveForm,
} from '../../../_components'

enum ObjectiveTabs {
  Details = 'details',
}

const tabs = [
  {
    key: ObjectiveTabs.Details,
    tab: 'Details',
  },
]

const ObjectiveDetailsPage = (props: {
  params: Promise<{ key: number; objectiveKey: number }>
}) => {
  const { key: planningIntervalKey, objectiveKey } = use(props.params)

  useDocumentTitle('PI Objective Details')

  const [activeTab, setActiveTab] = useState(ObjectiveTabs.Details)
  const [openUpdateObjectiveForm, setOpenUpdateObjectiveForm] =
    useState<boolean>(false)
  const [openDeleteObjectiveForm, setOpenDeleteObjectiveForm] =
    useState<boolean>(false)
  const [openCreateHealthCheckForm, setOpenCreateHealthCheckForm] =
    useState<boolean>(false)

  const {
    data: objectiveData,
    isLoading,
    refetch: refetchObjective,
  } = useGetPlanningIntervalObjectiveQuery({
    planningIntervalKey: planningIntervalKey.toString(),
    objectiveKey: objectiveKey.toString(),
  })

  const router = useRouter()
  const { hasPermissionClaim } = useAuth()
  const { hasLinkedEmployee } = useLinkedEmployee()
  const canManageObjectives = hasPermissionClaim(
    'Permissions.PlanningIntervalObjectives.Manage',
  )
  // A health check records who reported it, so it needs an employee link as well as the permission —
  // the API rejects an unlinked account with 403.
  const canCreateHealthChecks = !!canManageObjectives && hasLinkedEmployee
  const showActions = canManageObjectives

  const renderTabContent = () => {
    switch (activeTab) {
      case ObjectiveTabs.Details:
        return (
          <PlanningIntervalObjectiveDetails
            objective={objectiveData!}
            canManageObjectives={canManageObjectives}
          />
        )
      default:
        return null
    }
  }

  const onTabChange = (tabKey: string) => {
    setActiveTab(tabKey as ObjectiveTabs)
  }

  const onUpdateObjectiveFormClosed = (wasSaved: boolean) => {
    setOpenUpdateObjectiveForm(false)
    if (wasSaved) {
      refetchObjective()
    }
  }

  const onDeleteObjectiveFormClosed = (wasSaved: boolean) => {
    setOpenDeleteObjectiveForm(false)
    if (wasSaved) {
      // redirect to the PI details page
      router.push(`/planning/planning-intervals/${planningIntervalKey}`)
    }
  }

  const onCreateHealthCheckFormClosed = (wasSaved: boolean) => {
    setOpenCreateHealthCheckForm(false)
    if (wasSaved) {
      refetchObjective()
    }
  }

  const actionsMenuItems: MenuProps['items'] = (() => {
    const items: ItemType[] = []
    if (canManageObjectives) {
      items.push(
        {
          key: 'edit',
          label: 'Edit',
          onClick: () => setOpenUpdateObjectiveForm(true),
        },
        {
          key: 'delete',
          label: 'Delete',
          onClick: () => setOpenDeleteObjectiveForm(true),
        },
        {
          key: 'divider',
          type: 'divider',
        },
        {
          key: 'createHealthCheck',
          label: 'Create Health Check',
          disabled: !canCreateHealthChecks,
          onClick: () => setOpenCreateHealthCheckForm(true),
        },
      )
    }
    items.push({
      key: 'healthReport',
      label: (
        <Link
          href={`/planning/planning-intervals/${planningIntervalKey}/objectives/${objectiveKey}/health-report`}
        >
          Health Report
        </Link>
      ),
    })

    return items
  })()

  if (isLoading) {
    return <PlanningIntervalObjectiveDetailsLoading />
  }

  if (!isLoading && !objectiveData) {
    return notFound()
  }

  return (
    <>
      <PageTitle
        title={`${objectiveData?.key} - ${objectiveData?.name}`}
        subtitle="PI Objective Details"
        actions={<PageActions actionItems={actionsMenuItems} />}
        tags={
          <PiObjectiveHealthCheckTag
            healthCheck={objectiveData?.healthCheck}
            planningIntervalId={objectiveData?.planningInterval?.id}
            objectiveId={objectiveData?.id}
          />
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
      {openUpdateObjectiveForm && (
        <EditPlanningIntervalObjectiveForm
          objectiveKey={objectiveData!.key}
          planningIntervalKey={objectiveData!.planningInterval?.key}
          onFormSave={() => onUpdateObjectiveFormClosed(true)}
          onFormCancel={() => onUpdateObjectiveFormClosed(false)}
        />
      )}
      {openDeleteObjectiveForm && (
        <DeletePlanningIntervalObjectiveForm
          objective={objectiveData!}
          onFormSave={() => onDeleteObjectiveFormClosed(true)}
          onFormCancel={() => onDeleteObjectiveFormClosed(false)}
        />
      )}
      {openCreateHealthCheckForm && objectiveData && (
        <CreatePlanningIntervalObjectiveHealthCheckForm
          planningIntervalId={objectiveData.planningInterval.id}
          objectiveId={objectiveData.id}
          onFormCreate={() => onCreateHealthCheckFormClosed(true)}
          onFormCancel={() => onCreateHealthCheckFormClosed(false)}
        />
      )}
    </>
  )
}

const ObjectiveDetailsPageWithAuthorization = authorizePage(
  ObjectiveDetailsPage,
  'Permission',
  'Permissions.PlanningIntervals.View',
)

export default ObjectiveDetailsPageWithAuthorization

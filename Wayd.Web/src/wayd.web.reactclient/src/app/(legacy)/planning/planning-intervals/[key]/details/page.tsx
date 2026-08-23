'use client'

import PageTitle from '@/src/components/common/page-title'
import { Card } from 'antd'
import { createElement, use, useEffect, useState } from 'react'
import TeamsGrid, {
  TeamsGridProps,
} from '@/src/components/common/organizations/teams-grid'
import { useDocumentTitle } from '@/src/hooks/use-document-title'
import useAuth from '@/src/components/contexts/auth'
import { authorizePage } from '@/src/components/hoc'
import { notFound, usePathname } from 'next/navigation'
import { useAppDispatch } from '@/src/hooks'
import { setBreadcrumbTitle } from '@/src/store/breadcrumbs'
import PlanningIntervalDetailsLoading from '../loading'
import { PageActions } from '@/src/components/common'
import { ItemType } from 'antd/es/menu/interface'
import {
  EditPlanningIntervalForm,
  ManagePlanningIntervalDatesForm,
  ManagePlanningIntervalTeamsForm,
  PlanningIntervalDetails,
  PlanningIntervalTeamSprintMappings,
} from '../../_components'
import {
  useGetPlanningIntervalQuery,
  useGetPlanningIntervalTeamsQuery,
} from '@/src/store/features/planning/planning-interval-api'

enum PlanningIntervalTabs {
  Details = 'details',
  Teams = 'teams',
  SprintMappings = 'sprint-mappings',
}

const tabs = [
  {
    key: PlanningIntervalTabs.Details,
    tab: 'Details',
  },
  {
    key: PlanningIntervalTabs.Teams,
    tab: 'Teams',
  },
  {
    key: PlanningIntervalTabs.SprintMappings,
    tab: 'Sprint Mappings',
  },
]

const PlanningIntervalDetailsPage = (props: {
  params: Promise<{ key: string }>
}) => {
  const { key } = use(props.params)
  const piKey = Number(key)

  const [activeTab, setActiveTab] = useState(PlanningIntervalTabs.Details)
  const [openEditPlanningIntervalForm, setOpenEditPlanningIntervalForm] =
    useState<boolean>(false)
  const [teamsQueryEnabled, setTeamsQueryEnabled] = useState<boolean>(false)
  const [
    openManagePlanningIntervalDatesForm,
    setOpenManagePlanningIntervalDatesForm,
  ] = useState<boolean>(false)
  const [openManageTeamsForm, setOpenManageTeamsForm] = useState<boolean>(false)

  const pathname = usePathname()
  const dispatch = useAppDispatch()

  const { hasPermissionClaim } = useAuth()
  const canUpdatePlanningInterval = hasPermissionClaim(
    'Permissions.PlanningIntervals.Update',
  )

  const {
    data: planningIntervalData,
    isLoading,
    error,
    refetch: refetchPlanningInterval,
  } = useGetPlanningIntervalQuery(+piKey)

  useDocumentTitle(`${planningIntervalData?.name ?? piKey} - PI Details`)

  const {
    data: teamsData,
    isLoading: teamsIsLoading,
    refetch: refetchTeams,
  } = useGetPlanningIntervalTeamsQuery(+piKey, {
    skip: !teamsQueryEnabled,
  })

  const actionsMenuItems = (() => {
    const items = [] as ItemType[]
    if (canUpdatePlanningInterval) {
      items.push(
        {
          key: 'edit-pi-menu-item',
          label: 'Edit',
          onClick: () => setOpenEditPlanningIntervalForm(true),
        },
        {
          key: 'manage-dates-menu-item',
          label: 'Manage Dates',
          onClick: () => setOpenManagePlanningIntervalDatesForm(true),
        },
        {
          key: 'manage-teams-menu-item',
          label: 'Manage Teams',
          onClick: () => setOpenManageTeamsForm(true),
        },
      )
    }
    return items
  })()

  const renderTabContent = () => {
    switch (activeTab) {
      case PlanningIntervalTabs.Details:
        return (
          <PlanningIntervalDetails planningInterval={planningIntervalData!} />
        )
      case PlanningIntervalTabs.Teams:
        return createElement(TeamsGrid, {
          teams: teamsData,
          isLoading: teamsIsLoading,
          refetch: refetchTeams,
          persistStateKey: 'planning-interval-teams',
        } as TeamsGridProps)
      case PlanningIntervalTabs.SprintMappings:
        return (
          <PlanningIntervalTeamSprintMappings
            planningInterval={planningIntervalData!}
          />
        )
      default:
        return null
    }
  }

  useEffect(() => {
    planningIntervalData &&
      dispatch(
        setBreadcrumbTitle({ title: planningIntervalData.name, pathname }),
      )
  }, [dispatch, pathname, planningIntervalData])

  const onEditFormClosed = (wasSaved: boolean) => {
    setOpenEditPlanningIntervalForm(false)
    if (wasSaved) {
      refetchPlanningInterval()
    }
  }

  const onManagePlanningIntervalDatesFormClosed = (wasSaved: boolean) => {
    setOpenManagePlanningIntervalDatesForm(false)
    if (wasSaved) {
      refetchPlanningInterval()
    }
  }

  const onManageTeamsFormClosed = (wasSaved: boolean) => {
    setOpenManageTeamsForm(false)
    if (wasSaved) {
      refetchPlanningInterval()
      if (teamsQueryEnabled) {
        refetchTeams()
      }
    }
  }

  // doesn't trigger on first render
  const onTabChange = (tabKey: string) => {
    setActiveTab(tabKey as PlanningIntervalTabs)

    // enables the query for the tab on first render if it hasn't been enabled yet
    if (tabKey == PlanningIntervalTabs.Teams && !teamsQueryEnabled) {
      setTeamsQueryEnabled(true)
    }
  }

  if (isLoading) {
    return <PlanningIntervalDetailsLoading />
  }

  if (!isLoading && !planningIntervalData) {
    return notFound()
  }

  return (
    <>
      <PageTitle
        title="PI Details"
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
      {openEditPlanningIntervalForm && (
        <EditPlanningIntervalForm
          planningIntervalKey={piKey}
          onFormUpdate={() => onEditFormClosed(true)}
          onFormCancel={() => onEditFormClosed(false)}
        />
      )}
      {openManagePlanningIntervalDatesForm && (
        <ManagePlanningIntervalDatesForm
          id={planningIntervalData!.id}
          planningIntervalKey={piKey}
          onFormSave={() => onManagePlanningIntervalDatesFormClosed(true)}
          onFormCancel={() => onManagePlanningIntervalDatesFormClosed(false)}
        />
      )}
      {openManageTeamsForm && (
        <ManagePlanningIntervalTeamsForm
          id={planningIntervalData!.id}
          onFormSave={() => onManageTeamsFormClosed(true)}
          onFormCancel={() => onManageTeamsFormClosed(false)}
        />
      )}
    </>
  )
}

const PlanningIntervalDetailsPageWithAuthorization = authorizePage(
  PlanningIntervalDetailsPage,
  'Permission',
  'Permissions.PlanningIntervals.View',
)

export default PlanningIntervalDetailsPageWithAuthorization

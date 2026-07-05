'use client'

import PageTitle from '@/src/components/common/page-title'
import { Card, MenuProps, Spin } from 'antd'
import { CloseOutlined } from '@ant-design/icons'
import {
  createElement,
  use,
  useEffect,
  useState,
} from 'react'
import TeamDetails from '../_components/team-details'
import RisksGrid, {
  RisksGridProps,
} from '@/src/components/common/planning/risks-grid'
import { useDocumentTitle } from '@/src/hooks/use-document-title'
import useAuth from '@/src/components/contexts/auth'
import {
  useGetTeamDetailsQuery,
  useGetTeamHasEverBeenScrumQuery,
  useGetTeamMembershipsQuery,
  useGetTeamRisksQuery,
} from '@/src/store/features/organizations/team-api'
import { authorizePage } from '@/src/components/hoc'
import { notFound, usePathname } from 'next/navigation'
import { useAppDispatch } from '@/src/hooks'
import { setBreadcrumbTitle } from '@/src/store/breadcrumbs'
import TeamDependencyManagement from '../_components/team-dependency-management'
import { ItemType } from 'antd/es/menu/interface'
import { InactiveTag, PageActions } from '@/src/components/common'
import DeactivateTeamForm from '../../_components/deactivate-team-form'
import TeamSprints from '../_components/team-sprints'
import dynamic from 'next/dynamic'
import {
  SetTeamOperatingModelForm,
  TeamOperatingModelsGrid,
  EditTeamOperatingModelForm,
} from '../_components'
import { Methodology } from '@/src/services/wayd-api'
import {
  CreateTeamMembershipForm,
  EditTeamForm,
  TeamMembershipsGrid,
} from '../../_components'
import TeamMembersGrid from '../_components/team-members-grid'
import AddTeamMemberForm from '../_components/add-team-member-form'

const CycleTimeReport = dynamic(
  () =>
    import('@/src/components/common/work/cycle-time-report').then((mod) => ({
      default: mod.CycleTimeReport,
    })),
  { ssr: false, loading: () => <Spin /> },
)

const TeamBacklog = dynamic(() => import('../_components/team-backlog'), {
  ssr: false,
  loading: () => <Spin />,
})

enum TeamTabs {
  Details = 'details',
  Backlog = 'backlog',
  Sprints = 'sprints',
  DependencyManagement = 'dependency-management',
  RiskManagement = 'risk-management',
  TeamMemberships = 'team-memberships',
  Members = 'members',
  OperatingModelHistory = 'operating-model-history',
  CycleTimeReport = 'cycle-time-report',
}

const getStaticTabs = (isScrumTeam: boolean) => {
  const baseTabs = [
    {
      key: TeamTabs.Details,
      tab: 'Details',
    },
    {
      key: TeamTabs.Backlog,
      tab: 'Backlog',
    },
  ]

  if (isScrumTeam) {
    baseTabs.push({
      key: TeamTabs.Sprints,
      tab: 'Sprints',
    })
  }

  baseTabs.push(
    {
      key: TeamTabs.DependencyManagement,
      tab: 'Dependency Management',
    },
    {
      key: TeamTabs.RiskManagement,
      tab: 'Risk Management',
    },
    {
      key: TeamTabs.Members,
      tab: 'Members',
    },
    {
      key: TeamTabs.TeamMemberships,
      tab: 'Team Memberships',
    },
  )

  return baseTabs
}

const TeamDetailsPage = (props: { params: Promise<{ key: string }> }) => {
  const { key } = use(props.params)
  const teamKey = Number(key)

  const [activeTab, setActiveTab] = useState<TeamTabs>(TeamTabs.Details)
  const [openCreateTeamMembershipForm, setOpenCreateTeamMembershipForm] =
    useState<boolean>(false)
  const [openAddMemberForm, setOpenAddMemberForm] = useState<boolean>(false)
  const [openDeactivateTeamForm, setOpenDeactivateTeamForm] =
    useState<boolean>(false)
  const [openSetOperatingModelForm, setOpenSetOperatingModelForm] =
    useState<boolean>(false)
  const [openUpdateOperatingModelForm, setOpenUpdateOperatingModelForm] =
    useState<boolean>(false)
  const [teamMembershipsQueryEnabled, setTeamMembershipsQueryEnabled] =
    useState<boolean>(false)
  const [risksQueryEnabled, setRisksQueryEnabled] = useState<boolean>(false)
  const [includeClosedRisks, setIncludeClosedRisks] = useState<boolean>(false)
  const [dynamicTabs, setDynamicTabs] = useState<
    Array<{ key: string; tab: string; closable: boolean }>
  >([])

  const { hasPermissionClaim } = useAuth()
  const canUpdateTeam = hasPermissionClaim('Permissions.Teams.Update')
  const canManageTeamMemberships = hasPermissionClaim(
    'Permissions.Teams.ManageTeamMemberships',
  )

  const [isEditOpen, setIsEditOpen] = useState<boolean>(false)
  const {
    data: team,
    error,
    refetch: refetchTeam,
  } = useGetTeamDetailsQuery(teamKey)
  const teamNotFound = (error as any)?.status === 404
  const dispatch = useAppDispatch()
  const pathname = usePathname()
  const isScrumTeam = team?.operatingModel?.methodology === Methodology.Scrum

  const { data: hasEverBeenScrum } = useGetTeamHasEverBeenScrumQuery(team?.id ?? '', {
    skip: !team?.id,
  })

  const teamMembershipsQuery = useGetTeamMembershipsQuery(
    { teamId: team?.id ?? '', enabled: teamMembershipsQueryEnabled },
    { skip: !team?.id || !teamMembershipsQueryEnabled },
  )

  useDocumentTitle(`${team?.code ?? teamKey} - Team Details`)

  const risksQuery = useGetTeamRisksQuery(
    {
      id: team?.id ?? '',
      includeClosed: includeClosedRisks,
      enabled: risksQueryEnabled,
    },
    { skip: !team?.id || !risksQueryEnabled },
  )

  const onIncludeClosedRisksChanged = (includeClosed: boolean) => {
    setIncludeClosedRisks(includeClosed)
  }

  const openCycleTimeReport = () => {
    // Check if the cycle time report tab is already open
    const cycleTimeTabExists = dynamicTabs.some(
      (tab) => tab.key === TeamTabs.CycleTimeReport,
    )

    if (!cycleTimeTabExists) {
      // Add the tab to the end
      setDynamicTabs((prevTabs) => [
        ...prevTabs,
        {
          key: TeamTabs.CycleTimeReport,
          tab: 'Cycle Time Report',
          closable: true,
        },
      ])
    }

    // Switch to the cycle time report tab
    setActiveTab(TeamTabs.CycleTimeReport)
  }

  const openOperatingModelHistory = () => {
    const tabExists = dynamicTabs.some(
      (tab) => tab.key === TeamTabs.OperatingModelHistory,
    )

    if (!tabExists) {
      setDynamicTabs((prevTabs) => [
        ...prevTabs,
        {
          key: TeamTabs.OperatingModelHistory,
          tab: 'Operating Model History',
          closable: true,
        },
      ])
    }

    setActiveTab(TeamTabs.OperatingModelHistory)
  }

  const actionsMenuItems: MenuProps['items'] = (() => {
    const items: ItemType[] = []

    if (canUpdateTeam) {
      items.push({
        key: 'edit',
        label: 'Edit',
        onClick: () => setIsEditOpen(true),
      })

      if (team?.isActive === true) {
        items.push({
          key: 'deactivate',
          label: 'Deactivate',
          onClick: () => setOpenDeactivateTeamForm(true),
        })
      }
    }

    if (team?.isActive === true && (canUpdateTeam || canManageTeamMemberships)) {
      const teamManagementChildren: ItemType[] = []

      if (canUpdateTeam) {
        teamManagementChildren.push({
          key: 'add-member',
          label: 'Add Member',
          onClick: () => setOpenAddMemberForm(true),
        })
      }

      if (canManageTeamMemberships) {
        teamManagementChildren.push({
          key: 'add-team-membership',
          label: 'Add Team Membership',
          onClick: () => setOpenCreateTeamMembershipForm(true),
        })
      }

      items.push({ type: 'divider', key: 'divider-team-management' })
      items.push({
        type: 'group',
        label: 'Team Management',
        children: teamManagementChildren,
      })
    }

    if (canUpdateTeam && team?.isActive === true) {
      const operatingModelChildren: ItemType[] = []

      if (team?.operatingModel) {
        operatingModelChildren.push({
          key: 'update-operating-model',
          label: 'Update Operating Model',
          title: 'Updates the current operating model for the team',
          onClick: () => setOpenUpdateOperatingModelForm(true),
        })
      }

      operatingModelChildren.push({
        key: 'set-operating-model',
        label: 'Set Operating Model',
        title: 'Sets a new operating model for the team',
        onClick: () => setOpenSetOperatingModelForm(true),
      })

      items.push({ type: 'divider', key: 'divider-operating-model' })
      items.push({
        type: 'group',
        label: 'Operating Model',
        children: operatingModelChildren,
      })
    }

    items.push({ type: 'divider', key: 'divider-reports' })

    items.push({
      type: 'group',
      label: 'Reports',
      children: [
        {
          key: 'cycle-time-report',
          label: 'Cycle Time Report',
          onClick: openCycleTimeReport,
        },
        {
          key: 'operating-model-history',
          label: 'Operating Model History',
          onClick: openOperatingModelHistory,
        },
      ],
    })

    return items
  })()
  const renderTabContent = () => {
    switch (activeTab) {
      case TeamTabs.Details:
        return <TeamDetails team={team!} />
      case TeamTabs.Backlog:
        return <TeamBacklog teamId={team!.id!} />
      case TeamTabs.Sprints:
        return <TeamSprints teamId={team!.id!} />
      case TeamTabs.DependencyManagement:
        return <TeamDependencyManagement team={team!} />
      case TeamTabs.RiskManagement:
        return createElement(RisksGrid, {
          risks: risksQuery.data ?? [],
          updateIncludeClosed: onIncludeClosedRisksChanged,
          isLoadingRisks: risksQuery.isLoading,
          refreshRisks: risksQuery.refetch,
          newRisksAllowed: true,
          teamId: team!.id!,
          hideTeamColumn: true,
          persistStateKey: 'team-risks',
        } as RisksGridProps)
      case TeamTabs.TeamMemberships:
        return createElement(TeamMembershipsGrid, {
          teamId: team!.id!,
          teamMemberships: teamMembershipsQuery.data,
          isLoading: teamMembershipsQuery.isLoading,
          refetch: teamMembershipsQuery.refetch,
          teamType: 'Team',
        })
      case TeamTabs.OperatingModelHistory:
        return (
          <TeamOperatingModelsGrid
            teamId={team!.id!}
            canUpdate={canUpdateTeam}
          />
        )
      case TeamTabs.Members:
        return (
          <TeamMembersGrid
            teamId={team!.id!}
            teamType="Team"
          />
        )
      case TeamTabs.CycleTimeReport:
        return <CycleTimeReport teamCode={team!.code} />
      default:
        return null
    }
  }

  useEffect(() => {
    team && dispatch(setBreadcrumbTitle({ title: team.name, pathname }))
  }, [team, dispatch, pathname])

  // Redirect from Sprints tab if team has never been a Scrum team
  useEffect(() => {
    if (activeTab === TeamTabs.Sprints && hasEverBeenScrum === false) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setActiveTab(TeamTabs.Details)
    }
  }, [activeTab, hasEverBeenScrum])

  // doesn't trigger on first render
  const onTabChange = (tabKey: string) => {
    setActiveTab(tabKey as TeamTabs)

    // enables the query for the tab on first render if it hasn't been enabled yet
    if (tabKey == TeamTabs.RiskManagement && !risksQueryEnabled) {
      setRisksQueryEnabled(true)
    } else if (
      tabKey == TeamTabs.TeamMemberships &&
      !teamMembershipsQueryEnabled
    ) {
      setTeamMembershipsQueryEnabled(true)
    }
  }

  const closeTab = (tabKey: string, e: React.MouseEvent) => {
    e.stopPropagation()
    // Remove the tab from dynamicTabs
    setDynamicTabs((prevTabs) => prevTabs.filter((tab) => tab.key !== tabKey))

    // If the active tab is being closed, switch to Details tab
    if (activeTab === tabKey) {
      setActiveTab(TeamTabs.Details)
    }
  }

  const allTabs = (() => {
    const staticTabs = getStaticTabs(hasEverBeenScrum === true).map((tab) => ({
      key: tab.key,
      tab: tab.tab,
    }))

    const closableTabs = dynamicTabs.map((tab) => ({
      key: tab.key,
      tab: (
        <span>
          {tab.tab}
          <CloseOutlined
            style={{ marginLeft: 8 }}
            onClick={(e) => closeTab(tab.key, e)}
          />
        </span>
      ),
    }))

    return [...staticTabs, ...closableTabs]
  })()

  const onCreateTeamMembershipFormClosed = (wasSaved: boolean) => {
    setOpenCreateTeamMembershipForm(false)
    if (wasSaved) {
      refetchTeam()
    }
  }

  const onDeactivateTeamFormClosed = (wasSaved: boolean) => {
    setOpenDeactivateTeamForm(false)
    if (wasSaved) {
      refetchTeam()
    }
  }

  const onSetOperatingModelFormClosed = (wasSaved: boolean) => {
    setOpenSetOperatingModelForm(false)
    if (wasSaved) {
      refetchTeam()
    }
  }

  const onUpdateOperatingModelFormClosed = (wasSaved: boolean) => {
    setOpenUpdateOperatingModelForm(false)
    if (wasSaved) {
      refetchTeam()
    }
  }

  if (teamNotFound) {
    return notFound()
  }

  return (
    <>
      <PageTitle
        title={team?.name}
        subtitle="Team Details"
        tags={<InactiveTag isActive={team?.isActive ?? false} />}
        actions={<PageActions actionItems={actionsMenuItems} />}
      />
      <Card
        style={{ width: '100%' }}
        tabList={allTabs}
        activeTabKey={activeTab}
        onTabChange={onTabChange}
      >
        {renderTabContent()}
      </Card>
      {isEditOpen && team && canUpdateTeam && (
        <EditTeamForm
          team={team}
          open={isEditOpen}
          onClose={() => setIsEditOpen(false)}
        />
      )}
      {openCreateTeamMembershipForm && (
        <CreateTeamMembershipForm
          teamId={team!.id!}
          teamType={'Team'}
          onFormCreate={() => onCreateTeamMembershipFormClosed(true)}
          onFormCancel={() => onCreateTeamMembershipFormClosed(false)}
        />
      )}
      {openDeactivateTeamForm && (
        <DeactivateTeamForm
          team={team!}
          onFormComplete={() => onDeactivateTeamFormClosed(true)}
          onFormCancel={() => onDeactivateTeamFormClosed(false)}
        />
      )}
      {openSetOperatingModelForm && team && (
        <SetTeamOperatingModelForm
          teamId={team.id}
          onFormComplete={() => onSetOperatingModelFormClosed(true)}
          onFormCancel={() => onSetOperatingModelFormClosed(false)}
        />
      )}
      {openUpdateOperatingModelForm && team?.operatingModel && (
        <EditTeamOperatingModelForm
          teamId={team.id}
          operatingModelId={team.operatingModel.id}
          onFormComplete={() => onUpdateOperatingModelFormClosed(true)}
          onFormCancel={() => onUpdateOperatingModelFormClosed(false)}
        />
      )}
      {openAddMemberForm && team?.isActive && (
        <AddTeamMemberForm
          teamId={team.id!}
          teamType="Team"
          onFormComplete={() => setOpenAddMemberForm(false)}
          onFormCancel={() => setOpenAddMemberForm(false)}
        />
      )}
    </>
  )
}

const TeamDetailsPageWithAuthorization = authorizePage(
  TeamDetailsPage,
  'Permission',
  'Permissions.Teams.View',
)

export default TeamDetailsPageWithAuthorization

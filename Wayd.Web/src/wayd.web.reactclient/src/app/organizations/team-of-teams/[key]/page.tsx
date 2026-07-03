'use client'

import PageTitle from '@/src/components/common/page-title'
import { Card, MenuProps } from 'antd'
import {
  createElement,
  use,
  useEffect,
  useState,
} from 'react'
import TeamOfTeamsDetails from '../_components/team-of-teams-details'
import RisksGrid, {
  RisksGridProps,
} from '@/src/components/common/planning/risks-grid'
import { useDocumentTitle } from '@/src/hooks/use-document-title'
import { EditTeamForm, TeamMembershipsGrid } from '../../_components'
import TeamMembersGrid from '../../teams/_components/team-members-grid'
import AddTeamMemberForm from '../../teams/_components/add-team-member-form'
import useAuth from '@/src/components/contexts/auth'
import {
  useGetTeamOfTeamsDetailsQuery,
  useGetTeamOfTeamsMembershipsQuery,
  useGetTeamOfTeamsRisksQuery,
} from '@/src/store/features/organizations/team-api'
import { authorizePage } from '@/src/components/hoc'
import { notFound, usePathname } from 'next/navigation'
import { useAppDispatch } from '@/src/hooks'
import { setBreadcrumbTitle } from '@/src/store/breadcrumbs'
import { CreateTeamMembershipForm } from '../../_components'
import { InactiveTag, PageActions } from '@/src/components/common'
import { ItemType } from 'antd/es/menu/interface'
import DeactivateTeamOfTeamsForm from '../../_components/deactivate-team-of-teams-form'

enum TeamOfTeamsTabs {
  Details = 'details',
  RiskManagement = 'risk-management',
  TeamMemberships = 'team-memberships',
  Members = 'members',
}

const tabs = [
  {
    key: TeamOfTeamsTabs.Details,
    tab: 'Details',
  },
  {
    key: TeamOfTeamsTabs.RiskManagement,
    tab: 'Risk Management',
  },
  {
    key: TeamOfTeamsTabs.Members,
    tab: 'Members',
  },
  {
    key: TeamOfTeamsTabs.TeamMemberships,
    tab: 'Team Memberships',
  },
]

const TeamOfTeamsDetailsPage = (props: {
  params: Promise<{ key: string }>
}) => {
  const { key } = use(props.params)
  const teamKey = Number(key)

  useDocumentTitle('Team of Teams Details')

  const [activeTab, setActiveTab] = useState(TeamOfTeamsTabs.Details)
  const [openCreateTeamMembershipForm, setOpenCreateTeamMembershipForm] =
    useState<boolean>(false)
  const [openAddMemberForm, setOpenAddMemberForm] = useState<boolean>(false)
  const [openDeactivateTeamForm, setOpenDeactivateTeamForm] =
    useState<boolean>(false)
  const [teamMembershipsQueryEnabled, setTeamMembershipsQueryEnabled] =
    useState<boolean>(false)
  const [risksQueryEnabled, setRisksQueryEnabled] = useState<boolean>(false)
  const [includeClosedRisks, setIncludeClosedRisks] = useState<boolean>(false)

  const { hasClaim } = useAuth()
  const canUpdateTeam = hasClaim('Permission', 'Permissions.Teams.Update')
  const canManageTeamMemberships = hasClaim(
    'Permission',
    'Permissions.Teams.ManageTeamMemberships',
  )

  const [isEditOpen, setIsEditOpen] = useState<boolean>(false)
  const {
    data: team,
    error,
    refetch: refetchTeam,
  } = useGetTeamOfTeamsDetailsQuery(teamKey)
  const teamNotFound = (error as any)?.status === 404
  const dispatch = useAppDispatch()
  const pathname = usePathname()
  const teamMembershipsQuery = useGetTeamOfTeamsMembershipsQuery(
    { teamId: team?.id ?? '', enabled: teamMembershipsQueryEnabled },
    { skip: !team?.id || !teamMembershipsQueryEnabled },
  )

  const risksQuery = useGetTeamOfTeamsRisksQuery(
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

    return items
  })()
  const renderTabContent = () => {
    switch (activeTab) {
      case TeamOfTeamsTabs.Details:
        return <TeamOfTeamsDetails team={team!} />
      case TeamOfTeamsTabs.RiskManagement:
        return createElement(RisksGrid, {
          risks: risksQuery.data ?? [],
          updateIncludeClosed: onIncludeClosedRisksChanged,
          isLoadingRisks: risksQuery.isLoading,
          refreshRisks: risksQuery.refetch,
          newRisksAllowed: true,
          teamId: team?.id,
          hideTeamColumn: true,
        } as RisksGridProps)
      case TeamOfTeamsTabs.TeamMemberships:
        return createElement(TeamMembershipsGrid, {
          teamId: team?.id ?? '',
          teamMemberships: teamMembershipsQuery.data,
          isLoading: teamMembershipsQuery.isLoading,
          refetch: teamMembershipsQuery.refetch,
          teamType: 'Team of Teams',
        })
      case TeamOfTeamsTabs.Members:
        return (
          <TeamMembersGrid
            teamId={team?.id ?? ''}
            teamType="TeamOfTeams"
          />
        )
      default:
        return null
    }
  }

  useEffect(() => {
    team && dispatch(setBreadcrumbTitle({ title: team.name, pathname }))
  }, [team, dispatch, pathname])

  useEffect(() => {
    error && console.error(error)
  }, [error])

  // doesn't trigger on first render
  const onTabChange = (tabKey: string) => {
    setActiveTab(tabKey as TeamOfTeamsTabs)

    // enables the query for the tab on first render if it hasn't been enabled yet
    if (tabKey == TeamOfTeamsTabs.RiskManagement && !risksQueryEnabled) {
      setRisksQueryEnabled(true)
    } else if (
      tabKey == TeamOfTeamsTabs.TeamMemberships &&
      !teamMembershipsQueryEnabled
    ) {
      setTeamMembershipsQueryEnabled(true)
    }
  }

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

  if (teamNotFound) {
    return notFound()
  }

  const teamName = !team
    ? undefined
    : team.isActive
      ? team?.name
      : `${team?.name} (Inactive)`

  return (
    <>
      <PageTitle
        title={teamName}
        subtitle="Team of Teams Details"
        tags={<InactiveTag isActive={team?.isActive ?? false} />}
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
          teamType={'Team of Teams'}
          onFormCreate={() => onCreateTeamMembershipFormClosed(true)}
          onFormCancel={() => onCreateTeamMembershipFormClosed(false)}
        />
      )}
      {openDeactivateTeamForm && (
        <DeactivateTeamOfTeamsForm
          team={team!}
          onFormComplete={() => onDeactivateTeamFormClosed(true)}
          onFormCancel={() => onDeactivateTeamFormClosed(false)}
        />
      )}
      {openAddMemberForm && team?.isActive && (
        <AddTeamMemberForm
          teamId={team.id!}
          teamType="TeamOfTeams"
          onFormComplete={() => setOpenAddMemberForm(false)}
          onFormCancel={() => setOpenAddMemberForm(false)}
        />
      )}
    </>
  )
}

const TeamOfTeamsDetailsPageWithAuthorization = authorizePage(
  TeamOfTeamsDetailsPage,
  'Permission',
  'Permissions.Teams.View',
)

export default TeamOfTeamsDetailsPageWithAuthorization

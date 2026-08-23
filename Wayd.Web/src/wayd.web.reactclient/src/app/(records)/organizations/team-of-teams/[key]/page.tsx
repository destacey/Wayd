'use client'

import PageTitle from '@/src/components/common/page-title'
import { ClusterOutlined } from '@ant-design/icons'
import { MenuProps } from 'antd'
import {
  createElement,
  use,
  useEffect,
  useState,
} from 'react'
import TeamOfTeamsDetails from '@/src/app/(legacy)/organizations/team-of-teams/_components/team-of-teams-details'
import RisksGrid, {
  RisksGridProps,
} from '@/src/components/common/planning/risks-grid'
import { useDocumentTitle } from '@/src/hooks/use-document-title'
import { EditTeamForm, TeamMembershipsGrid } from '@/src/app/(legacy)/organizations/_components'
import TeamMembersGrid from '@/src/app/(legacy)/organizations/teams/_components/team-members-grid'
import AddTeamMemberForm from '@/src/app/(legacy)/organizations/teams/_components/add-team-member-form'
import useAuth from '@/src/components/contexts/auth'
import {
  useGetTeamOfTeamsDetailsQuery,
  useGetTeamOfTeamsMembershipsQuery,
  useGetTeamOfTeamsRisksQuery,
} from '@/src/store/features/organizations/team-api'
import { authorizePage } from '@/src/components/hoc'
import { notFound, usePathname, useSearchParams } from 'next/navigation'
import TeamOfTeamDetailsLoading from './loading'
import { useAppDispatch } from '@/src/hooks'
import { setBreadcrumbTitle } from '@/src/store/breadcrumbs'
import { CreateTeamMembershipForm } from '@/src/app/(legacy)/organizations/_components'
import { InactiveTag, PageActions } from '@/src/components/common'
import { RecordLayout, RecordSection } from '@/src/components/common/record'
import { ItemType } from 'antd/es/menu/interface'
import DeactivateTeamOfTeamsForm from '@/src/app/(legacy)/organizations/_components/deactivate-team-of-teams-form'

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

  // The active section lives in the URL (?section=), owned by RecordLayout.
  // Read here only to gate the queries that load lazily.
  const searchParams = useSearchParams()
  const activeTab = (searchParams.get('section') ??
    TeamOfTeamsTabs.Details) as TeamOfTeamsTabs
  const [openCreateTeamMembershipForm, setOpenCreateTeamMembershipForm] =
    useState<boolean>(false)
  const [openAddMemberForm, setOpenAddMemberForm] = useState<boolean>(false)
  const [openDeactivateTeamForm, setOpenDeactivateTeamForm] =
    useState<boolean>(false)
  // Expensive sections do not fetch until open — including on arrival via a
  // deep link, since this reads the URL. No visited-latch: RTK Query caches by
  // args, so returning to a section serves the cached result.
  const risksQueryEnabled = activeTab === TeamOfTeamsTabs.RiskManagement
  const teamMembershipsQueryEnabled =
    activeTab === TeamOfTeamsTabs.TeamMemberships
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
  const renderSectionContent = (activeTab: TeamOfTeamsTabs) => {
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
          persistStateKey: 'team-of-teams-risks',
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

  const sections: RecordSection[] = [
    { id: TeamOfTeamsTabs.Details, label: 'Details' },
    { id: TeamOfTeamsTabs.RiskManagement, label: 'Risks' },
    { id: TeamOfTeamsTabs.Members, label: 'Members' },
    { id: TeamOfTeamsTabs.TeamMemberships, label: 'Team Memberships' },
  ]

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

  // Sections dereference `team` directly, and RecordLayout renders the active
  // section immediately — including on a deep link straight into one, which
  // arrives before the query resolves. Hold the page until the record loads
  // rather than making every section null-safe.
  if (!team) {
    return <TeamOfTeamDetailsLoading />
  }

  const teamName = !team
    ? undefined
    : team.isActive
      ? team?.name
      : `${team?.name} (Inactive)`

  return (
    <>
      <RecordLayout
        sections={sections}
        defaultSection={TeamOfTeamsTabs.Details}
        header={
          <PageTitle
            title={teamName}
            subtitle="Team of Teams Details"
            parent={{
              label: 'Team of Teams',
              href: '/organizations/team-of-teams',
            }}
            // The code, not the numeric key — see the teams page.
            recordKey={team?.code}
            avatar={{ kind: 'record', icon: <ClusterOutlined /> }}
            tags={<InactiveTag isActive={team?.isActive ?? false} />}
            actions={<PageActions actionItems={actionsMenuItems} />}
          />
        }
      >
        {(section) => renderSectionContent(section as TeamOfTeamsTabs)}
      </RecordLayout>
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

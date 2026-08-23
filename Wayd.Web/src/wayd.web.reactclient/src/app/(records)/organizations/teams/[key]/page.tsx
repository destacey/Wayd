'use client'

import PageTitle from '@/src/components/common/page-title'
import { MenuProps, Spin } from 'antd'
import { TeamOutlined } from '@ant-design/icons'
import {
  createElement,
  use,
  useEffect,

  useState,
} from 'react'
import TeamDetails from '@/src/app/(legacy)/organizations/teams/_components/team-details'
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
import {
  notFound,
  usePathname,
  useRouter,
  useSearchParams,
} from 'next/navigation'
import { useAppDispatch } from '@/src/hooks'
import { setBreadcrumbTitle } from '@/src/store/breadcrumbs'
import TeamDependencyManagement from '@/src/app/(legacy)/organizations/teams/_components/team-dependency-management'
import { ItemType } from 'antd/es/menu/interface'
import { InactiveTag, PageActions } from '@/src/components/common'
import { RecordLayout, RecordSection } from '@/src/components/common/record'
import DeactivateTeamForm from '@/src/app/(legacy)/organizations/_components/deactivate-team-form'
import TeamSprints from '@/src/app/(legacy)/organizations/teams/_components/team-sprints'
import dynamic from 'next/dynamic'
import {
  SetTeamOperatingModelForm,
  TeamOperatingModelsGrid,
  EditTeamOperatingModelForm,
} from '@/src/app/(legacy)/organizations/teams/_components'
import { Methodology } from '@/src/services/wayd-api'
import {
  CreateTeamMembershipForm,
  EditTeamForm,
  TeamMembershipsGrid,
} from '@/src/app/(legacy)/organizations/_components'
import TeamMembersGrid from '@/src/app/(legacy)/organizations/teams/_components/team-members-grid'
import TeamDetailsLoading from './loading'
import AddTeamMemberForm from '@/src/app/(legacy)/organizations/teams/_components/add-team-member-form'

const CycleTimeReport = dynamic(
  () =>
    import('@/src/components/common/work/cycle-time-report').then((mod) => ({
      default: mod.CycleTimeReport,
    })),
  { ssr: false, loading: () => <Spin /> },
)

const TeamBacklog = dynamic(() => import('@/src/app/(legacy)/organizations/teams/_components/team-backlog'), {
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

const TeamDetailsPage = (props: { params: Promise<{ key: string }> }) => {
  const { key } = use(props.params)
  const teamKey = Number(key)

  // The active section lives in the URL (?section=), owned by RecordLayout.
  // Read here only to enable the queries that load lazily on first visit.
  const searchParams = useSearchParams()
  const activeTab = (searchParams.get('section') ?? TeamTabs.Details) as TeamTabs

  const [openCreateTeamMembershipForm, setOpenCreateTeamMembershipForm] =
    useState<boolean>(false)
  const [openAddMemberForm, setOpenAddMemberForm] = useState<boolean>(false)
  const [openDeactivateTeamForm, setOpenDeactivateTeamForm] =
    useState<boolean>(false)
  const [openSetOperatingModelForm, setOpenSetOperatingModelForm] =
    useState<boolean>(false)
  const [openUpdateOperatingModelForm, setOpenUpdateOperatingModelForm] =
    useState<boolean>(false)
  const [includeClosedRisks, setIncludeClosedRisks] = useState<boolean>(false)

  // Expensive sections do not fetch until their section is open — including on
  // arrival via a deep link, since this reads the URL rather than a click.
  // No "have I visited this" latch is needed: RTK Query caches by args, so
  // returning to a section serves the cached result instead of refetching.
  const risksQueryEnabled = activeTab === TeamTabs.RiskManagement
  const teamMembershipsQueryEnabled = activeTab === TeamTabs.TeamMemberships

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
  const router = useRouter()
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

  // Reports are permanent entries in the rail's Reports group, so opening one
  // is navigation rather than tab bookkeeping.
  const goToSection = (section: TeamTabs) =>
    router.replace(`${pathname}?section=${section}`, { scroll: false })

  const openCycleTimeReport = () => goToSection(TeamTabs.CycleTimeReport)

  const openOperatingModelHistory = () =>
    goToSection(TeamTabs.OperatingModelHistory)

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
  const renderSectionContent = (activeTab: TeamTabs) => {
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

  // Sprints is only meaningful for teams that have run Scrum; a deep link to it
  // otherwise lands on Details rather than an empty section.
  useEffect(() => {
    if (activeTab === TeamTabs.Sprints && hasEverBeenScrum === false) {
      router.replace(pathname, { scroll: false })
    }
  }, [activeTab, hasEverBeenScrum, router, pathname])

  const sections: RecordSection[] = (() => {
    const items: RecordSection[] = [
      { id: TeamTabs.Details, label: 'Details' },
      { id: TeamTabs.Backlog, label: 'Backlog' },
    ]
    if (hasEverBeenScrum === true) {
      items.push({ id: TeamTabs.Sprints, label: 'Sprints' })
    }
    items.push(
      { id: TeamTabs.DependencyManagement, label: 'Dependencies' },
      { id: TeamTabs.RiskManagement, label: 'Risks' },
      { id: TeamTabs.Members, label: 'Members' },
      { id: TeamTabs.TeamMemberships, label: 'Team Memberships' },
    )
    return items
  })()

  // Reports were closable tabs appended to the strip; in the rail they are a
  // named group, addressable by URL like any other section.
  const reports: RecordSection[] = [
    { id: TeamTabs.CycleTimeReport, label: 'Cycle Time' },
    { id: TeamTabs.OperatingModelHistory, label: 'Operating Model History' },
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

  // Sections dereference `team` directly, and RecordLayout renders the active
  // section immediately — including on a deep link straight to ?section=backlog,
  // which arrives before the query resolves. Hold the whole page until the
  // record is loaded rather than making every section null-safe.
  if (!team) {
    return <TeamDetailsLoading />
  }

  return (
    <>
      <RecordLayout
        sections={sections}
        reports={reports}
        defaultSection={TeamTabs.Details}
        header={
          <PageTitle
            title={team?.name}
            subtitle="Team Details"
            parent={{ label: 'Teams', href: '/organizations/teams' }}
            // The code, not the numeric key — it is what people say out loud,
            // and the numeric key is already available in the details section.
            recordKey={team?.code}
            avatar={{ kind: 'record', icon: <TeamOutlined /> }}
            tags={<InactiveTag isActive={team?.isActive ?? false} />}
            actions={<PageActions actionItems={actionsMenuItems} />}
          />
        }
      >
        {(section) => renderSectionContent(section as TeamTabs)}
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

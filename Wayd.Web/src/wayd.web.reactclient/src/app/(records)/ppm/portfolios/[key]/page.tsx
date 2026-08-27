'use client'

import { LifecycleStatusTag, PageActions } from '@/src/components/common'
import { RecordLayout, RecordSection } from '@/src/components/common/record'
import useAuth from '@/src/components/contexts/auth'
import { authorizePage } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import {
  useGetPortfolioProgramsQuery,
  useGetPortfolioProjectsQuery,
  useGetPortfolioQuery,
  useGetPortfolioRankingScoreboardQuery,
  useGetPortfolioStrategicInitiativesQuery,
} from '@/src/store/features/ppm/portfolios-api'
import { MenuProps } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import { notFound, useRouter, useSearchParams } from 'next/navigation'
import { Suspense, use, useState } from 'react'
import {
  DeletePortfolioForm,
  EditPortfolioForm,
} from '@/src/app/(legacy)/ppm/portfolios/_components'
import ChangePortfolioStatusForm, {
  PortfolioStatusAction,
} from '@/src/app/(legacy)/ppm/portfolios/_components/change-portfolio-status-form'
import SetPortfolioScoringModelForm from './_components/set-portfolio-scoring-model-form'
import ProjectRankingBoard from './_components/ranking/project-ranking-board'
import {
  ProgramsFilterBar,
  ProgramViewManager,
  ProjectsFilterBar,
  ProjectViewManager,
  StrategicInitiativesFilterBar,
  StrategicInitiativeViewManager,
} from '@/src/app/(legacy)/ppm/_components'
import { useStatusFilter } from '../../_components/use-status-filter'
import { canActOnPpmRecord } from '../../_components/ppm-authorization'
import PortfolioDetailsLoading from './loading'
import PortfolioFacts from './_components/portfolio-facts'
import PortfolioOverview, {
  OverviewTab,
} from './_components/portfolio-overview'

enum PortfolioSections {
  Overview = 'overview',
  Programs = 'programs',
  Projects = 'projects',
  StrategicInitiatives = 'strategic-initiatives',
  Ranking = 'ranking',
}

// Non-closed project statuses for the ranking board: Proposed(1), Approved(5), Active(2)
// (excludes Completed(3) and Canceled(4)).
const RANKING_STATUSES = [1, 5, 2]

/** Active(2) — the programs a portfolio is currently delivering through. */
const DEFAULT_PROGRAM_STATUSES = [2]
/** Approved(5), Active(2) — the projects actually in flight. */
const DEFAULT_PROJECT_STATUSES = [5, 2]
/** Approved(2), Active(3) — initiatives use their own status ids. */
const DEFAULT_SI_STATUSES = [2, 3]

enum MenuActions {
  Edit = 'Edit',
  Delete = 'Delete',
  Activate = 'Activate',
  Close = 'Close',
  Archive = 'Archive',
  SetScoringModel = 'Set Scoring Model',
}

const PortfolioDetailsPage = (props: { params: Promise<{ key: string }> }) => {
  const { key } = use(props.params)
  const portfolioKey = Number(key)

  const [openEditPortfolioForm, setOpenEditPortfolioForm] =
    useState<boolean>(false)
  const [openActivatePortfolioForm, setOpenActivatePortfolioForm] =
    useState<boolean>(false)
  const [openClosePortfolioForm, setOpenClosePortfolioForm] =
    useState<boolean>(false)
  const [openArchivePortfolioForm, setOpenArchivePortfolioForm] =
    useState<boolean>(false)
  const [openDeletePortfolioForm, setOpenDeletePortfolioForm] =
    useState<boolean>(false)
  const [openSetScoringModelForm, setOpenSetScoringModelForm] =
    useState<boolean>(false)

  const router = useRouter()

  const { hasPermissionClaim } = useAuth()
  const canUpdatePortfolio = hasPermissionClaim(
    'Permissions.ProjectPortfolios.Update',
  )
  const canDeletePortfolio = hasPermissionClaim(
    'Permissions.ProjectPortfolios.Delete',
  )

  // Each filter is shared by the overview's tiles and charts and by the
  // section that lists what it counts, so the two can never disagree. Keyed by
  // portfolio, so filtering one does not change what another opens on.
  const { selected: programStatuses, setSelected: setProgramStatuses } =
    useStatusFilter(
      `portfolio:${portfolioKey}:programStatus`,
      DEFAULT_PROGRAM_STATUSES,
    )
  const { selected: projectStatuses, setSelected: setProjectStatuses } =
    useStatusFilter(
      `portfolio:${portfolioKey}:projectStatus`,
      DEFAULT_PROJECT_STATUSES,
    )
  const { selected: siStatuses, setSelected: setSiStatuses } = useStatusFilter(
    `portfolio:${portfolioKey}:initiativeStatus`,
    DEFAULT_SI_STATUSES,
  )

  // The active section lives in the URL, owned by RecordLayout. Read here only
  // to hold back the ranking queries, which the overview does not summarise.
  const searchParams = useSearchParams()
  const activeSection = (searchParams.get('section') ??
    PortfolioSections.Overview) as PortfolioSections

  // Which collection the overview is reporting on. A second axis of state
  // alongside the section, so it travels in the URL the same way — a link can
  // point at the portfolio's initiatives rather than always opening programs.
  const requestedTab = searchParams.get('summary')
  const overviewTab = Object.values(OverviewTab).includes(
    requestedTab as OverviewTab,
  )
    ? (requestedTab as OverviewTab)
    : OverviewTab.Projects

  const setOverviewTab = (tab: OverviewTab) => {
    const params = new URLSearchParams(searchParams.toString())
    if (tab === OverviewTab.Projects) {
      params.delete('summary')
    } else {
      params.set('summary', tab)
    }
    const query = params.toString()
    router.replace(
      query
        ? `/ppm/portfolios/${portfolioKey}?${query}`
        : `/ppm/portfolios/${portfolioKey}`,
      { scroll: false },
    )
  }

  const {
    data: portfolioData,
    isLoading,
    refetch: refetchPortfolio,
  } = useGetPortfolioQuery(portfolioKey)

  const canManagePortfolio = canActOnPpmRecord(
    canUpdatePortfolio,
    portfolioData?.canManagePortfolio,
  )

  // Deleting takes the same leadership, paired with its own claim rather than
  // Update's.
  const canDelete = canActOnPpmRecord(
    canDeletePortfolio,
    portfolioData?.canManagePortfolio,
  )

  // Ranking is restricted to the same set (per the PPM docs — drag-to-rank is Owners/Managers only).
  const canManageRanking = canManagePortfolio

  // The overview counts all three collections, so they load with the record
  // rather than waiting for their section.
  const {
    data: programData,
    isLoading: isLoadingPrograms,
    refetch: refetchPrograms,
  } = useGetPortfolioProgramsQuery({
    portfolioIdOrKey: portfolioKey.toString(),
    status: programStatuses.length > 0 ? programStatuses : undefined,
  })

  const {
    data: projectData,
    isLoading: isLoadingProjects,
    refetch: refetchProjects,
  } = useGetPortfolioProjectsQuery({
    portfolioIdOrKey: portfolioKey.toString(),
    status: projectStatuses.length > 0 ? projectStatuses : undefined,
  })

  const {
    data: strategicInitiativeData,
    isLoading: isLoadingStrategicInitiatives,
    refetch: refetchStrategicInitiatives,
  } = useGetPortfolioStrategicInitiativesQuery({
    portfolioIdOrKey: portfolioKey.toString(),
    status: siStatuses.length > 0 ? siStatuses : undefined,
  })

  // Ranking runs its own fixed status set and is nothing the overview reports,
  // so it stays behind its section.
  const isRanking = activeSection === PortfolioSections.Ranking

  const {
    data: rankingData,
    isLoading: isLoadingRanking,
    refetch: refetchRanking,
  } = useGetPortfolioProjectsQuery(
    {
      portfolioIdOrKey: portfolioKey.toString(),
      status: RANKING_STATUSES,
    },
    { skip: !isRanking },
  )

  const { data: rankingScoreboard, refetch: refetchRankingScoreboard } =
    useGetPortfolioRankingScoreboardQuery(portfolioData?.id ?? '', {
      skip: !isRanking || !portfolioData?.id,
    })

  useDocumentTitle(`${portfolioData?.name ?? portfolioKey} - Portfolio Details`)

  const actionsMenuItems: MenuProps['items'] = (() => {
    const currentStatus = portfolioData?.status.name
    const availableActions =
      currentStatus === 'Proposed'
        ? [MenuActions.Delete, MenuActions.Activate]
        : currentStatus === 'Active'
          ? [MenuActions.Close]
          : currentStatus === 'Closed'
            ? [MenuActions.Archive]
            : []

    // TODO: Implement On Hold status

    const items: ItemType[] = []
    if (canManagePortfolio && currentStatus !== 'Archived') {
      items.push({
        key: 'edit',
        label: MenuActions.Edit,
        onClick: () => setOpenEditPortfolioForm(true),
      })
    }
    if (canDelete && availableActions.includes(MenuActions.Delete)) {
      items.push({
        key: 'delete',
        label: MenuActions.Delete,
        onClick: () => setOpenDeletePortfolioForm(true),
      })
    }

    const canSetScoringModel = canManagePortfolio && currentStatus !== 'Archived'

    const hasManageActions =
      canSetScoringModel ||
      (canManagePortfolio &&
        (availableActions.includes(MenuActions.Activate) ||
          availableActions.includes(MenuActions.Close) ||
          availableActions.includes(MenuActions.Archive)))

    if (hasManageActions && items.length > 0) {
      items.push({
        key: 'manage-divider',
        type: 'divider',
      })
    }

    if (canSetScoringModel) {
      items.push({
        key: 'set-scoring-model',
        label: MenuActions.SetScoringModel,
        onClick: () => setOpenSetScoringModelForm(true),
      })
    }

    if (canManagePortfolio && availableActions.includes(MenuActions.Activate)) {
      items.push({
        key: 'activate',
        label: MenuActions.Activate,
        onClick: () => setOpenActivatePortfolioForm(true),
      })
    }

    if (canManagePortfolio && availableActions.includes(MenuActions.Close)) {
      items.push({
        key: 'close',
        label: MenuActions.Close,
        onClick: () => setOpenClosePortfolioForm(true),
      })
    }

    if (canManagePortfolio && availableActions.includes(MenuActions.Archive)) {
      items.push({
        key: 'archive',
        label: MenuActions.Archive,
        onClick: () => setOpenArchivePortfolioForm(true),
      })
    }

    return items
  })()

  const onEditPortfolioFormClosed = (wasSaved: boolean) => {
    setOpenEditPortfolioForm(false)
    if (wasSaved) {
      refetchPortfolio()
    }
  }

  const onActivatePortfolioFormClosed = (wasSaved: boolean) => {
    setOpenActivatePortfolioForm(false)
    if (wasSaved) {
      refetchPortfolio()
    }
  }

  const onClosePortfolioFormClosed = (wasSaved: boolean) => {
    setOpenClosePortfolioForm(false)
    if (wasSaved) {
      refetchPortfolio()
    }
  }

  const onArchivePortfolioFormClosed = (wasSaved: boolean) => {
    setOpenArchivePortfolioForm(false)
    if (wasSaved) {
      refetchPortfolio()
    }
  }

  const onSetScoringModelFormClosed = (wasSaved: boolean) => {
    setOpenSetScoringModelForm(false)
    if (wasSaved) {
      refetchPortfolio()
    }
  }

  const onDeletePortfolioFormClosed = (wasDeleted: boolean) => {
    setOpenDeletePortfolioForm(false)
    if (wasDeleted) {
      router.push('/ppm/portfolios')
    }
  }

  if (isLoading) {
    return <PortfolioDetailsLoading />
  }

  if (!portfolioData) {
    return notFound()
  }

  const goToSection = (sectionId: string) => {
    const params = new URLSearchParams(searchParams.toString())
    if (sectionId === PortfolioSections.Overview) {
      params.delete('section')
    } else {
      params.set('section', sectionId)
    }
    const query = params.toString()
    router.replace(
      query
        ? `/ppm/portfolios/${portfolioKey}?${query}`
        : `/ppm/portfolios/${portfolioKey}`,
      { scroll: false },
    )
  }

  // One bar per collection, rendered both by the overview tab that summarises
  // it and by the section that lists it. Built once rather than per caller:
  // the same filter shown twice with different props reads as two filters.
  const filterBars = {
    [OverviewTab.Programs]: (
      <ProgramsFilterBar
        selectedStatuses={programStatuses}
        onStatusChange={setProgramStatuses}
        showPortfolioFilter={false}
        onReset={() => setProgramStatuses(DEFAULT_PROGRAM_STATUSES)}
      />
    ),
    [OverviewTab.Projects]: (
      <ProjectsFilterBar
        selectedStatuses={projectStatuses}
        onStatusChange={setProjectStatuses}
        showPortfolioFilter={false}
        // Nothing on this page is wired to the role filter, so it would be a
        // control that does nothing.
        showRoleFilter={false}
        onReset={() => setProjectStatuses(DEFAULT_PROJECT_STATUSES)}
      />
    ),
    [OverviewTab.StrategicInitiatives]: (
      <StrategicInitiativesFilterBar
        selectedStatuses={siStatuses}
        onStatusChange={setSiStatuses}
        showPortfolioFilter={false}
        onReset={() => setSiStatuses(DEFAULT_SI_STATUSES)}
      />
    ),
  }

  const sections: RecordSection[] = [
    { id: PortfolioSections.Overview, label: 'Overview' },
    {
      id: PortfolioSections.Programs,
      label: 'Programs',
      count: programData?.length,
    },
    {
      id: PortfolioSections.Projects,
      label: 'Projects',
      count: projectData?.length,
    },
    {
      id: PortfolioSections.StrategicInitiatives,
      label: 'Strategic Initiatives',
      count: strategicInitiativeData?.length,
    },
    { id: PortfolioSections.Ranking, label: 'Ranking' },
  ]

  const renderSection = (section: PortfolioSections) => {
    switch (section) {
      case PortfolioSections.Programs:
        return (
          <>
            {filterBars[OverviewTab.Programs]}
            <ProgramViewManager
              programs={programData ?? []}
              isLoading={isLoadingPrograms}
              refetch={refetchPrograms}
            />
          </>
        )
      case PortfolioSections.Projects:
        return (
          <>
            {filterBars[OverviewTab.Projects]}
            <ProjectViewManager
              projects={projectData ?? []}
              isLoading={isLoadingProjects}
              refetch={refetchProjects}
              groupByProgram={true}
              persistStateKey="portfolio-projects"
            />
          </>
        )
      case PortfolioSections.StrategicInitiatives:
        return (
          <>
            {filterBars[OverviewTab.StrategicInitiatives]}
            <StrategicInitiativeViewManager
              strategicInitiatives={strategicInitiativeData ?? []}
              isLoading={isLoadingStrategicInitiatives}
              refetch={refetchStrategicInitiatives}
            />
          </>
        )
      case PortfolioSections.Ranking:
        return (
          <ProjectRankingBoard
            portfolioId={portfolioData.id}
            portfolioKey={portfolioKey}
            projects={rankingData ?? []}
            scoreboard={rankingScoreboard}
            canManage={canManageRanking}
            isLoading={isLoadingRanking}
            refetch={refetchRanking}
            refetchScoreboard={refetchRankingScoreboard}
          />
        )
      default:
        return (
          <PortfolioOverview
            activeTab={overviewTab}
            onTabChange={setOverviewTab}
            filterBar={filterBars[overviewTab]}
            programs={programData ?? []}
            programsLoading={isLoadingPrograms}
            projects={projectData ?? []}
            projectsLoading={isLoadingProjects}
            strategicInitiatives={strategicInitiativeData ?? []}
            strategicInitiativesLoading={isLoadingStrategicInitiatives}
            onNavigateToSection={goToSection}
          />
        )
    }
  }

  return (
    <>
      <RecordLayout
        sections={sections}
        defaultSection={PortfolioSections.Overview}
        record={{
          name: portfolioData.name,
          recordKey: String(portfolioData.key),
          parent: { label: 'Portfolios', href: '/ppm/portfolios' },
          subtitle: 'Portfolio Details',
          tags: <LifecycleStatusTag status={portfolioData.status} />,
          actions: <PageActions actionItems={actionsMenuItems} />,
        }}
        facts={<PortfolioFacts portfolio={portfolioData} />}
      >
        {(section) => renderSection(section as PortfolioSections)}
      </RecordLayout>

      {openEditPortfolioForm && (
        <EditPortfolioForm
          portfolioKey={portfolioData.key}
          onFormComplete={() => onEditPortfolioFormClosed(true)}
          onFormCancel={() => onEditPortfolioFormClosed(false)}
        />
      )}
      {openActivatePortfolioForm && (
        <ChangePortfolioStatusForm
          portfolio={portfolioData}
          statusAction={PortfolioStatusAction.Activate}
          onFormComplete={() => onActivatePortfolioFormClosed(true)}
          onFormCancel={() => onActivatePortfolioFormClosed(false)}
        />
      )}
      {openClosePortfolioForm && (
        <ChangePortfolioStatusForm
          portfolio={portfolioData}
          statusAction={PortfolioStatusAction.Close}
          onFormComplete={() => onClosePortfolioFormClosed(true)}
          onFormCancel={() => onClosePortfolioFormClosed(false)}
        />
      )}
      {openArchivePortfolioForm && (
        <ChangePortfolioStatusForm
          portfolio={portfolioData}
          statusAction={PortfolioStatusAction.Archive}
          onFormComplete={() => onArchivePortfolioFormClosed(true)}
          onFormCancel={() => onArchivePortfolioFormClosed(false)}
        />
      )}
      {openDeletePortfolioForm && (
        <DeletePortfolioForm
          portfolio={portfolioData}
          onFormComplete={() => onDeletePortfolioFormClosed(true)}
          onFormCancel={() => onDeletePortfolioFormClosed(false)}
        />
      )}
      {openSetScoringModelForm && (
        <SetPortfolioScoringModelForm
          portfolioId={portfolioData.id}
          portfolioKey={portfolioData.key}
          scoringModelId={portfolioData.scoringModel?.id}
          onFormComplete={() => onSetScoringModelFormClosed(true)}
          onFormCancel={() => onSetScoringModelFormClosed(false)}
        />
      )}
    </>
  )
}

// useSearchParams suspends a prerendered route up to the nearest boundary. In
// development routes render on demand, so a missing one only fails the
// production build.
const PortfolioDetailsPageWithSuspense = (props: {
  params: Promise<{ key: string }>
}) => (
  <Suspense fallback={<PortfolioDetailsLoading />}>
    <PortfolioDetailsPage {...props} />
  </Suspense>
)

const PortfolioDetailsPageWithAuthorization = authorizePage(
  PortfolioDetailsPageWithSuspense,
  'Permission',
  'Permissions.ProjectPortfolios.View',
)

export default PortfolioDetailsPageWithAuthorization

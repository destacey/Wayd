'use client'

import {
  LifecycleStatusTag,
  PageActions,
  PageTitle,
} from '@/src/components/common'
import useAuth from '@/src/components/contexts/auth'
import { authorizePage } from '@/src/components/hoc'
import { useAppDispatch, useDocumentTitle } from '@/src/hooks'
import { Card, MenuProps } from 'antd'
import { notFound, usePathname, useRouter } from 'next/navigation'
import PortfolioDetailsLoading from './loading'
import { use, useEffect, useState } from 'react'
import { BreadcrumbItem, setBreadcrumbRoute } from '@/src/store/breadcrumbs'
import { ItemType } from 'antd/es/menu/interface'
import {
  DeletePortfolioForm,
  EditPortfolioForm,
  PortfolioDetails,
} from '../_components'
import {
  useGetPortfolioProgramsQuery,
  useGetPortfolioProjectsQuery,
  useGetPortfolioQuery,
  useGetPortfolioStrategicInitiativesQuery,
} from '@/src/store/features/ppm/portfolios-api'
import ChangePortfolioStatusForm, {
  PortfolioStatusAction,
} from '../_components/change-portfolio-status-form'
import SetPortfolioScoringModelForm from './_components/set-portfolio-scoring-model-form'
import {
  ProgramsFilterBar,
  ProgramViewManager,
  ProjectsFilterBar,
  ProjectViewManager,
  StrategicInitiativesFilterBar,
  StrategicInitiativeViewManager,
} from '../../_components'

enum PortfolioTabs {
  Details = 'details',
  Programs = 'programs',
  Projects = 'projects',
  StrategicInitiatives = 'strategicInitiatives',
}

const tabs = [
  {
    key: PortfolioTabs.Details,
    label: 'Details',
  },
  {
    key: PortfolioTabs.Programs,
    label: 'Programs',
  },
  {
    key: PortfolioTabs.Projects,
    label: 'Projects',
  },
  {
    key: PortfolioTabs.StrategicInitiatives,
    label: 'Strategic Initiatives',
  },
]

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

  const [activeTab, setActiveTab] = useState(PortfolioTabs.Details)
  const [programsQueried, setProgramsQueried] = useState(false)
  const [selectedProgramStatuses, setSelectedProgramStatuses] = useState<
    number[]
  >([2]) // Active
  const [projectsQueried, setProjectsQueried] = useState(false)
  const [selectedProjectStatuses, setSelectedProjectStatuses] = useState<
    number[]
  >([5, 2]) // Approved, Active
  const [strategicInitiativesQueried, setStrategicInitiativesQueried] =
    useState(false)
  const [selectedSIStatuses, setSelectedSIStatuses] = useState<number[]>([
    2, 3,
  ]) // Approved, Active

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

  const pathname = usePathname()
  const dispatch = useAppDispatch()

  const router = useRouter()

  const { hasPermissionClaim } = useAuth()
  const canUpdatePortfolio = hasPermissionClaim(
    'Permissions.ProjectPortfolios.Update',
  )
  const canDeletePortfolio = hasPermissionClaim(
    'Permissions.ProjectPortfolios.Delete',
  )

  const {
    data: portfolioData,
    isLoading,
    error,
    refetch: refetchPortfolio,
  } = useGetPortfolioQuery(portfolioKey)

  const {
    data: programData,
    isLoading: isLoadingPrograms,
    error: errorPrograms,
    refetch: refetchPrograms,
  } = useGetPortfolioProgramsQuery(
    {
      portfolioIdOrKey: portfolioKey.toString(),
      status:
        selectedProgramStatuses.length > 0
          ? selectedProgramStatuses
          : undefined,
    },
    { skip: !programsQueried },
  )

  const {
    data: projectData,
    isLoading: isLoadingProjects,
    error: errorProjects,
    refetch: refetchProjects,
  } = useGetPortfolioProjectsQuery(
    {
      portfolioIdOrKey: portfolioKey.toString(),
      status:
        selectedProjectStatuses.length > 0
          ? selectedProjectStatuses
          : undefined,
    },
    { skip: !projectsQueried },
  )

  const {
    data: strategicInitiativeData,
    isLoading: isLoadingStrategicInitiatives,
    error: errorStrategicInitiatives,
    refetch: refetchStrategicInitiatives,
  } = useGetPortfolioStrategicInitiativesQuery(
    {
      portfolioIdOrKey: portfolioKey.toString(),
      status: selectedSIStatuses.length > 0 ? selectedSIStatuses : undefined,
    },
    { skip: !strategicInitiativesQueried },
  )

  useDocumentTitle(`${portfolioData?.name ?? portfolioKey} - Portfolio Details`)

  useEffect(() => {
    if (!portfolioData) return

    const breadcrumbRoute: BreadcrumbItem[] = [
      {
        title: 'PPM',
      },
      {
        href: `/ppm/portfolios`,
        title: 'Portfolios',
      },
    ]

    breadcrumbRoute.push({
      title: 'Details',
    })

    dispatch(setBreadcrumbRoute({ route: breadcrumbRoute, pathname }))
  }, [dispatch, pathname, portfolioData])

  const handleProgramStatusChange = (statuses: number[]) => {
    setSelectedProgramStatuses(statuses)
  }

  const handleSIStatusChange = (statuses: number[]) => {
    setSelectedSIStatuses(statuses)
  }

  const handleProjectStatusChange = (statuses: number[]) => {
    setSelectedProjectStatuses(statuses)
  }

  const renderTabContent = () => {
    switch (activeTab) {
      case PortfolioTabs.Details:
        return <PortfolioDetails portfolio={portfolioData!} />
      case PortfolioTabs.Programs:
        return (
          <>
            <ProgramsFilterBar
              selectedStatuses={selectedProgramStatuses}
              onStatusChange={handleProgramStatusChange}
              showPortfolioFilter={false}
            />
            <ProgramViewManager
              programs={programData ?? []}
              isLoading={isLoadingPrograms}
              refetch={refetchPrograms}
            />
          </>
        )
      case PortfolioTabs.Projects:
        return (
          <>
            <ProjectsFilterBar
              selectedStatuses={selectedProjectStatuses}
              onStatusChange={handleProjectStatusChange}
              showPortfolioFilter={false}
            />
            <ProjectViewManager
              projects={projectData ?? []}
              isLoading={isLoadingProjects}
              refetch={refetchProjects}
              groupByProgram={true}
            />
          </>
        )
      case PortfolioTabs.StrategicInitiatives:
        return (
          <>
            <StrategicInitiativesFilterBar
              selectedStatuses={selectedSIStatuses}
              onStatusChange={handleSIStatusChange}
              showPortfolioFilter={false}
            />
            <StrategicInitiativeViewManager
              strategicInitiatives={strategicInitiativeData ?? []}
              isLoading={isLoadingStrategicInitiatives}
              refetch={refetchStrategicInitiatives}
            />
          </>
        )
      default:
        return null
    }
  }

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
    if (canUpdatePortfolio && currentStatus !== 'Archived') {
      items.push({
        key: 'edit',
        label: MenuActions.Edit,
        onClick: () => setOpenEditPortfolioForm(true),
      })
    }
    if (canDeletePortfolio && availableActions.includes(MenuActions.Delete)) {
      items.push({
        key: 'delete',
        label: MenuActions.Delete,
        onClick: () => setOpenDeletePortfolioForm(true),
      })
    }

    const canSetScoringModel =
      canUpdatePortfolio && currentStatus !== 'Archived'

    const hasManageActions =
      canSetScoringModel ||
      (canUpdatePortfolio &&
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

    if (canUpdatePortfolio && availableActions.includes(MenuActions.Activate)) {
      items.push({
        key: 'activate',
        label: MenuActions.Activate,
        onClick: () => setOpenActivatePortfolioForm(true),
      })
    }

    if (canUpdatePortfolio && availableActions.includes(MenuActions.Close)) {
      items.push({
        key: 'close',
        label: MenuActions.Close,
        onClick: () => setOpenClosePortfolioForm(true),
      })
    }

    if (canUpdatePortfolio && availableActions.includes(MenuActions.Archive)) {
      items.push({
        key: 'archive',
        label: MenuActions.Archive,
        onClick: () => setOpenArchivePortfolioForm(true),
      })
    }

    return items
  })()

  // doesn't trigger on first render
  const onTabChange = (tabKey: string) => {
    const tab = tabKey as PortfolioTabs

    if (tab === PortfolioTabs.Programs && !programsQueried) {
      setProgramsQueried(true)
    } else if (tab === PortfolioTabs.Projects && !projectsQueried) {
      setProjectsQueried(true)
    } else if (
      tab === PortfolioTabs.StrategicInitiatives &&
      !strategicInitiativesQueried
    ) {
      setStrategicInitiativesQueried(true)
    }

    setActiveTab(tab)
  }

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

  return (
    <>
      <PageTitle
        title={`${portfolioData?.key} - ${portfolioData?.name}`}
        subtitle="Portfolio Details"
        tags={<LifecycleStatusTag status={portfolioData?.status} />}
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

      {openEditPortfolioForm && (
        <EditPortfolioForm
          portfolioKey={portfolioData?.key}
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
      {openSetScoringModelForm && portfolioData && (
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

const PortfolioDetailsPageWithAuthorization = authorizePage(
  PortfolioDetailsPage,
  'Permission',
  'Permissions.ProjectPortfolios.View',
)

export default PortfolioDetailsPageWithAuthorization

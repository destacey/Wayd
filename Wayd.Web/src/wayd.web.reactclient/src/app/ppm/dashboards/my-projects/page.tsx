'use client'

import { PageTitle, UnlinkedEmployeeAlert } from '@/src/components/common'
import { authorizePage } from '@/src/components/hoc'
import {
  useAppDispatch,
  useDocumentTitle,
  useLocalStorageState,
  useRemainingHeight,
} from '@/src/hooks'
import { BreadcrumbItem, setBreadcrumbRoute } from '@/src/store/breadcrumbs'
import { useGetProjectsQuery } from '@/src/store/features/ppm/projects-api'
import { QuestionCircleOutlined } from '@ant-design/icons'
import { Button, Drawer, Grid, Tour } from 'antd'
import { WaydTooltip } from '@/src/components/common'
import { usePathname } from 'next/navigation'
import { FC, useEffect, useState } from 'react'
import MyProjectsDashboardFilterBar from './_components/filter-bar'
import MyProjectsSummaryBar from './_components/summary-bar'
import PortfolioGroupList from './_components/portfolio-group-list'
import ProjectDetailPanel from './_components/project-detail-panel'
import { useMyProjectsTour } from './_components/use-my-projects-tour'
import styles from './my-projects-dashboard.module.css'
import { useMessage } from '@/src/components/contexts/messaging'

const { useBreakpoint } = Grid

const PROJECT_STATUS = {
  Proposed: 1,
  Approved: 5,
  Active: 2,
  Completed: 3,
  Canceled: 4,
} as const

const DEFAULT_STATUSES = [PROJECT_STATUS.Approved, PROJECT_STATUS.Active]
const ALL_ROLES = [1, 2, 3, 4, 5]

const getRoleFilterValues = (selectedRoles: number[]): number[] | undefined => {
  if (selectedRoles.length === 0) return ALL_ROLES
  return selectedRoles
}

const MyProjectsPage: FC = () => {
  useDocumentTitle('My Projects')
  const dispatch = useAppDispatch()
  const pathname = usePathname()
  const messageApi = useMessage()
  const [selectedStatuses, setSelectedStatuses] = useLocalStorageState<
    number[]
  >('my-projects-filter-statuses', DEFAULT_STATUSES)
  const [selectedRoles, setSelectedRoles] = useLocalStorageState<number[]>(
    'my-projects-filter-roles',
    [],
  )
  const screens = useBreakpoint()
  const isMobile = !screens.md
  const [layoutRef, layoutHeight] = useRemainingHeight()

  const [selectedProjectKey, setSelectedProjectKey] = useState<string | null>(
    null,
  )

  const {
    refs: { filterBarRef, summaryBarRef, leftPanelRef, rightPanelRef },
    tourOpen,
    tourSteps,
    onTourClose,
    onTourStart,
    detailStepIndex,
  } = useMyProjectsTour()

  const {
    data: projects,
    isLoading,
    error,
    refetch,
  } = useGetProjectsQuery({
    status: selectedStatuses.length > 0 ? selectedStatuses : undefined,
    role: getRoleFilterValues(selectedRoles),
  })

  const handleTourStepChange = (current: number) => {
    if (
      current === detailStepIndex &&
      !selectedProjectKey &&
      projects?.length
    ) {
      setSelectedProjectKey(projects[0].key)
    }
  }

  useEffect(() => {
    if (error) {
      console.error(error)
      messageApi.error('Failed to load projects.')
    }
  }, [error, messageApi])

  useEffect(() => {
    const breadcrumbRoute: BreadcrumbItem[] = [
      { title: 'PPM' },
      { title: 'Projects', href: '/ppm/projects' },
      { title: 'My Projects' },
    ]
    dispatch(setBreadcrumbRoute({ route: breadcrumbRoute, pathname }))
  }, [dispatch, pathname])

  const handleStatusChange = (statuses: number[]) => {
    setSelectedStatuses(statuses)
    setSelectedProjectKey(null)
  }

  const handleRoleChange = (roles: number[]) => {
    setSelectedRoles(roles)
    setSelectedProjectKey(null)
  }

  const handleResetFilters = () => {
    setSelectedStatuses(DEFAULT_STATUSES)
    setSelectedRoles([])
    setSelectedProjectKey(null)
  }

  return (
    <>
      <PageTitle
        title="My Projects"
        actions={
          <WaydTooltip title="Take a tour">
            <Button
              type="text"
              shape="circle"
              icon={<QuestionCircleOutlined />}
              aria-label="Start dashboard tour"
              onClick={onTourStart}
            />
          </WaydTooltip>
        }
      />
      <UnlinkedEmployeeAlert consequence="Projects are assigned to employees, so this dashboard has nothing to show." />
      <MyProjectsDashboardFilterBar
        selectedRoles={selectedRoles}
        onRoleChange={handleRoleChange}
        selectedStatuses={selectedStatuses}
        onStatusChange={handleStatusChange}
        onReset={handleResetFilters}
        onRefresh={refetch}
        containerRef={filterBarRef}
      />
      <MyProjectsSummaryBar
        projectCount={projects?.length ?? 0}
        selectedStatuses={selectedStatuses}
        selectedRoles={selectedRoles}
        isLoading={isLoading}
        containerRef={summaryBarRef}
      />
      {isMobile ? (
        <>
          <div ref={leftPanelRef}>
            <PortfolioGroupList
              projects={projects}
              isLoading={isLoading}
              selectedProjectKey={selectedProjectKey}
              selectedRoles={selectedRoles}
              onSelectProject={setSelectedProjectKey}
            />
          </div>
          <Drawer
            title="Project Details"
            placement="bottom"
            size="85vh"
            onClose={() => setSelectedProjectKey(null)}
            open={!!selectedProjectKey}
            styles={{ body: { padding: 12 } }}
          >
            <div ref={rightPanelRef}>
              <ProjectDetailPanel projectKey={selectedProjectKey} />
            </div>
          </Drawer>
        </>
      ) : (
        <div
          ref={layoutRef}
          className={styles.layout}
          style={{ height: layoutHeight }}
        >
          <div ref={leftPanelRef} className={styles.leftPanel}>
            <PortfolioGroupList
              projects={projects}
              isLoading={isLoading}
              selectedProjectKey={selectedProjectKey}
              selectedRoles={selectedRoles}
              onSelectProject={setSelectedProjectKey}
            />
          </div>
          <div ref={rightPanelRef} className={styles.rightPanel}>
            <ProjectDetailPanel projectKey={selectedProjectKey} />
          </div>
        </div>
      )}
      <Tour
        open={tourOpen}
        onClose={onTourClose}
        onChange={handleTourStepChange}
        steps={tourSteps}
      />
    </>
  )
}

const MyProjectsPageWithAuthorization = authorizePage(
  MyProjectsPage,
  'Permission',
  'Permissions.Projects.View',
)

export default MyProjectsPageWithAuthorization

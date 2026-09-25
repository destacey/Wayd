'use client'

import { PageTitle, UnlinkedEmployeeAlert } from '@/src/components/common'
import BasicBreadcrumb from '@/src/components/common/basic-breadcrumb'
import { useMessage } from '@/src/components/contexts/messaging'
import { authorizePage } from '@/src/components/hoc'
import {
  useDebounce,
  useDocumentTitle,
  useLinkedEmployee,
  useLocalStorageState,
  useRemainingHeight,
} from '@/src/hooks'
import ProjectDrawer from '@/src/app/ppm/_components/project-drawer'
import { Grid } from 'antd'
import dayjs from 'dayjs'
import { usePathname, useRouter, useSearchParams } from 'next/navigation'
import { FC, useEffect, useState } from 'react'
import AttentionTiles from './_components/attention-tiles'
import BreakdownStrip from './_components/breakdown-strip'
import DashboardToolbar, {
  DashboardView,
} from './_components/dashboard-toolbar'
import {
  AttentionFilter,
  computeAttention,
  DashboardScope,
  DEFAULT_STATUSES,
  GroupBy,
  groupProjects,
  isPersonScope,
  matchesAttention,
  matchesSearch,
  scopeFromSearchParams,
  scopeToSearchParams,
  SortBy,
} from './_components/dashboard-model'
import ProjectsDashboardCards from './_components/projects-dashboard-cards'
import ProjectsDashboardGrid from './_components/projects-dashboard-grid'
import ProjectsDashboardTimeline from './_components/projects-dashboard-timeline'
import ScopeBar from './_components/scope-bar'
import { useScopedProjects } from './_components/use-scoped-projects'

const { useBreakpoint } = Grid

const ProjectsDashboardPage: FC = () => {
  useDocumentTitle('Projects Dashboard')
  const messageApi = useMessage()
  const router = useRouter()
  const pathname = usePathname()
  const searchParams = useSearchParams()
  const { employeeId: myEmployeeId, hasLinkedEmployee } = useLinkedEmployee()
  const screens = useBreakpoint()
  const isMobile = !screens.md
  const [listRef, listHeight] = useRemainingHeight()

  // The scope lives in the URL so a view of someone's or something's projects
  // can be shared; the filters stay local, as the other PPM filter bars keep theirs.
  const scope = scopeFromSearchParams(searchParams, hasLinkedEmployee)

  const setScope = (next: DashboardScope) => {
    const query = scopeToSearchParams(next, searchParams).toString()
    router.replace(query ? `${pathname}?${query}` : pathname)
    setSelectedProjectKey(null)
  }

  const [selectedStatuses, setSelectedStatuses] = useLocalStorageState<
    number[]
  >('projects-dashboard-filter-statuses', DEFAULT_STATUSES)
  const [selectedRoles, setSelectedRoles] = useLocalStorageState<number[]>(
    'projects-dashboard-filter-roles',
    [],
  )
  const [groupBy, setGroupBy] = useLocalStorageState<GroupBy>(
    'projects-dashboard-group-by',
    'portfolio',
  )
  const [sortBy, setSortBy] = useLocalStorageState<SortBy>(
    'projects-dashboard-sort-by',
    'attention',
  )
  const [view, setView] = useLocalStorageState<DashboardView>(
    'projects-dashboard-view',
    'List',
  )
  const [breakdownsExpanded, setBreakdownsExpanded] =
    useLocalStorageState<boolean>('projects-dashboard-breakdowns', false)
  const [attention, setAttention] = useState<AttentionFilter>('all')
  const [search, setSearch] = useState('')
  const debouncedSearch = useDebounce(search, 300)
  const [selectedProjectKey, setSelectedProjectKey] = useState<string | null>(
    null,
  )

  const {
    projects,
    planSummaries,
    isLoading,
    isSummariesLoading,
    error,
    refetch,
    subjectEmployeeId,
  } = useScopedProjects(scope, selectedStatuses, selectedRoles, myEmployeeId)

  useEffect(() => {
    if (error) {
      console.error(error)
      messageApi.error('Failed to load projects.')
    }
  }, [error, messageApi])

  const today = dayjs()
  const inScope = projects ?? []
  const counts = computeAttention(inScope, planSummaries, today)
  // The grid searches for itself; the dashboard's search box narrows only the
  // cards and the timeline.
  const attentionFiltered = inScope.filter((p) =>
    matchesAttention(p, attention, planSummaries, today),
  )
  const shown = attentionFiltered.filter((p) =>
    matchesSearch(p, debouncedSearch),
  )
  const groups = groupProjects(shown, groupBy, sortBy, planSummaries)

  const clearSelection = () => setSelectedProjectKey(null)

  const handleReset = () => {
    setSelectedStatuses(DEFAULT_STATUSES)
    setSelectedRoles([])
    setAttention('all')
    setSearch('')
    clearSelection()
  }

  const GroupedBody =
    view === 'Card' ? ProjectsDashboardCards : ProjectsDashboardTimeline

  return (
    <div className="page-gutters">
      <BasicBreadcrumb
        items={[
          { title: 'PPM' },
          { title: 'Projects', href: '/ppm/projects' },
          { title: 'Dashboard' },
        ]}
      />
      <PageTitle title="Projects Dashboard" />
      <UnlinkedEmployeeAlert consequence="Projects are assigned to employees, so the Me scope has nothing to show; pick a person, portfolio or program instead." />
      <ScopeBar
        scope={scope}
        onScopeChange={setScope}
        hasLinkedEmployee={hasLinkedEmployee}
        selectedRoles={selectedRoles}
        onRoleChange={(roles) => {
          setSelectedRoles(roles)
          clearSelection()
        }}
        selectedStatuses={selectedStatuses}
        onStatusChange={(statuses) => {
          setSelectedStatuses(statuses)
          clearSelection()
        }}
        onReset={handleReset}
        onRefresh={refetch}
      />
      <AttentionTiles
        counts={counts}
        active={attention}
        onChange={(filter) => {
          setAttention(filter)
          clearSelection()
        }}
        // The overdue tile is built from the task counts, so the row waits for both.
        isLoading={isLoading || isSummariesLoading}
      />
      {/* Breakdowns describe a population; a person's handful of projects is not one. */}
      {!isPersonScope(scope) && (
        <BreakdownStrip
          projects={inScope}
          isLoading={isLoading}
          expanded={breakdownsExpanded}
          onExpandedChange={setBreakdownsExpanded}
        />
      )}
      {/* The grid brings its own toolbar (search, count, export, columns), so
          the List view puts Group by and the view switch in it rather than
          stacking a second bar above. Cards and the timeline have no toolbar of
          their own, so they keep this one, search included. */}
      {view !== 'List' && (
        <DashboardToolbar
          groupBy={groupBy}
          onGroupByChange={setGroupBy}
          sortBy={sortBy}
          onSortByChange={setSortBy}
          search={search}
          onSearchChange={setSearch}
          view={view}
          onViewChange={setView}
          shownCount={shown.length}
          totalCount={inScope.length}
        />
      )}
      <div ref={listRef}>
        {view === 'List' ? (
          <ProjectsDashboardGrid
            projects={attentionFiltered}
            groupBy={groupBy}
            onGroupByChange={setGroupBy}
            view={view}
            onViewChange={setView}
            planSummaries={planSummaries}
            employeeId={subjectEmployeeId}
            selectedProjectKey={selectedProjectKey}
            onSelectProject={setSelectedProjectKey}
            isLoading={isLoading}
            today={today}
            height={isMobile ? undefined : listHeight}
            onRefresh={refetch}
          />
        ) : (
          <GroupedBody
            groups={groups}
            planSummaries={planSummaries}
            employeeId={subjectEmployeeId}
            selectedProjectKey={selectedProjectKey}
            onSelectProject={setSelectedProjectKey}
            isLoading={isLoading}
            today={today}
            height={isMobile ? undefined : listHeight}
          />
        )}
      </div>
      {selectedProjectKey && (
        <ProjectDrawer
          projectKey={selectedProjectKey}
          drawerOpen={!!selectedProjectKey}
          onDrawerClose={clearSelection}
        />
      )}
    </div>
  )
}

const ProjectsDashboardPageWithAuthorization = authorizePage(
  ProjectsDashboardPage,
  'Permission',
  'Permissions.Projects.View',
)

export default ProjectsDashboardPageWithAuthorization

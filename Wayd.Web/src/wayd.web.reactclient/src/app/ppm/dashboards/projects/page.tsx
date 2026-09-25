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
import {
  useGetProjectsPlanSummariesQuery,
  useGetProjectsQuery,
} from '@/src/store/features/ppm/projects-api'
import ProjectDrawer from '@/src/app/ppm/_components/project-drawer'
import { Grid } from 'antd'
import dayjs from 'dayjs'
import { usePathname, useRouter, useSearchParams } from 'next/navigation'
import { FC, useEffect, useState } from 'react'
import AttentionTiles from './_components/attention-tiles'
import DashboardToolbar from './_components/dashboard-toolbar'
import {
  ALL_ROLES,
  AttentionFilter,
  computeAttention,
  DashboardScope,
  DEFAULT_STATUSES,
  GroupBy,
  groupProjects,
  matchesAttention,
  matchesSearch,
  SortBy,
} from './_components/dashboard-model'
import ProjectsDashboardList from './_components/projects-dashboard-list'
import ScopeBar from './_components/scope-bar'

const { useBreakpoint } = Grid

/** The `?employee=` query parameter that puts the page in person scope. */
const EMPLOYEE_PARAM = 'employee'

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

  // The scope lives in the URL so a view of someone's projects can be shared;
  // the filters stay local, as the other PPM filter bars keep theirs.
  const employeeParam = searchParams.get(EMPLOYEE_PARAM)
  const scope: DashboardScope =
    employeeParam !== null || !hasLinkedEmployee
      ? { kind: 'person', employeeId: employeeParam || null }
      : { kind: 'me' }

  const setScope = (next: DashboardScope) => {
    const params = new URLSearchParams(searchParams.toString())
    if (next.kind === 'me') {
      params.delete(EMPLOYEE_PARAM)
    } else {
      params.set(EMPLOYEE_PARAM, next.employeeId ?? '')
    }
    const query = params.toString()
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
  const [attention, setAttention] = useState<AttentionFilter>('all')
  const [search, setSearch] = useState('')
  const debouncedSearch = useDebounce(search, 300)
  const [selectedProjectKey, setSelectedProjectKey] = useState<string | null>(
    null,
  )

  const subjectEmployeeId =
    scope.kind === 'me' ? myEmployeeId : scope.employeeId
  // Me needs no explicit employee: the server resolves the principal, and the
  // linkage it sees may be newer than the token this page decoded.
  const employeeIdArg =
    scope.kind === 'person' ? (scope.employeeId ?? undefined) : undefined
  const hasSubject = subjectEmployeeId !== null

  const {
    data: projects,
    isLoading,
    error,
    refetch,
  } = useGetProjectsQuery(
    {
      status: selectedStatuses.length > 0 ? selectedStatuses : undefined,
      role: selectedRoles.length > 0 ? selectedRoles : ALL_ROLES,
      employeeId: employeeIdArg,
    },
    { skip: !hasSubject },
  )

  const projectIds = projects?.map((p) => p.id) ?? []
  const { data: planSummaries } = useGetProjectsPlanSummariesQuery(
    {
      projectIds,
      role: selectedRoles.length > 0 ? selectedRoles : undefined,
      employeeId: employeeIdArg,
    },
    { skip: projectIds.length === 0 },
  )

  useEffect(() => {
    if (error) {
      console.error(error)
      messageApi.error('Failed to load projects.')
    }
  }, [error, messageApi])

  const today = dayjs()
  const summaries = planSummaries ?? {}
  const inScope = projects ?? []
  const counts = computeAttention(inScope, summaries, today)
  const shown = inScope.filter(
    (p) =>
      matchesAttention(p, attention, summaries, today) &&
      matchesSearch(p, debouncedSearch),
  )
  const groups = groupProjects(shown, groupBy, sortBy, summaries)

  const clearSelection = () => setSelectedProjectKey(null)

  const handleReset = () => {
    setSelectedStatuses(DEFAULT_STATUSES)
    setSelectedRoles([])
    setAttention('all')
    setSearch('')
    clearSelection()
  }

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
      <UnlinkedEmployeeAlert consequence="Projects are assigned to employees, so the Me scope has nothing to show; pick a person instead." />
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
        isLoading={hasSubject && isLoading}
      />
      <DashboardToolbar
        groupBy={groupBy}
        onGroupByChange={setGroupBy}
        sortBy={sortBy}
        onSortByChange={setSortBy}
        search={search}
        onSearchChange={setSearch}
        shownCount={shown.length}
        totalCount={inScope.length}
      />
      <div ref={listRef}>
        <ProjectsDashboardList
          groups={groups}
          planSummaries={summaries}
          employeeId={subjectEmployeeId}
          selectedProjectKey={selectedProjectKey}
          onSelectProject={setSelectedProjectKey}
          isLoading={hasSubject && isLoading}
          today={today}
          height={isMobile ? undefined : listHeight}
        />
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

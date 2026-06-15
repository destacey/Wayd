import { ExportOutlined, MoreOutlined } from '@ant-design/icons'
import { Button, Dropdown } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import './wayd-grid-cell-renderers.css'
import PiObjectiveHealthCheckTag from '../../app/planning/planning-intervals/_components/pi-objective-health-check-tag'
import ProjectHealthCheckTag from '../../app/ppm/projects/_components/project-health-check-tag'
import { HealthCheckStatusTagData } from './health-check/health-check-tag'
import {
  LifecycleNavigationDto,
  NavigationDto,
  PlanningIntervalObjectiveListDto,
  SimpleNavigationDto,
  UserDetailsDto,
  WorkIterationNavigationDto,
} from '@/src/services/wayd-api'
import Link from 'next/link'
import { MarkdownRenderer } from './markdown'
import { CustomCellRendererProps } from 'ag-grid-react'
import WorkStatusTag from './work/work-status-tag'
import { DependencyHealth, WorkStatusCategory } from '../types'
import DependencyHealthTag from './work/dependency-health-tag'
import { LifecycleStatusTag } from '.'

export interface HealthCheckStatusColumn {
  id: string
  status: SimpleNavigationDto
  reportedOn: Date
  expiration: Date
}

export const NestedPiObjectiveHealthCheckStatusCellRenderer = <
  T extends {
    id: string
    healthCheck: HealthCheckStatusColumn | null
    planningInterval?: { id: string }
  },
>(
  props: CustomCellRendererProps<T>,
) => {
  const { data } = props
  if (!data?.healthCheck) return null
  return (
    <PiObjectiveHealthCheckTag
      healthCheck={data.healthCheck}
      planningIntervalId={data.planningInterval?.id}
      objectiveId={data.id}
    />
  )
}

export const PiObjectiveHealthCheckStatusCellRenderer = (
  props: CustomCellRendererProps<HealthCheckStatusColumn>,
) => {
  const { data } = props
  if (!data) return null
  return <PiObjectiveHealthCheckTag healthCheck={data} />
}

export const NestedProjectHealthCheckStatusCellRenderer = <
  T extends {
    id: string
    healthCheck?: (HealthCheckStatusTagData & { id: string }) | null
  },
>(
  props: CustomCellRendererProps<T>,
) => {
  const { data } = props
  if (!data?.healthCheck) return null
  return (
    <ProjectHealthCheckTag
      healthCheck={data.healthCheck}
      projectId={data.id}
    />
  )
}

export const ProjectHealthCheckStatusCellRenderer = (
  props: CustomCellRendererProps<HealthCheckStatusTagData & { id: string }>,
) => {
  const { data } = props
  if (!data) return null
  return <ProjectHealthCheckTag healthCheck={data} />
}

export const WorkStatusTagCellRenderer = <
  T extends { status: string; statusCategory: { id: WorkStatusCategory } },
>(
  props: CustomCellRendererProps<T>,
) => {
  const { value, data } = props
  if (!data?.status) return null
  return <WorkStatusTag status={value} category={data.statusCategory.id} />
}

export const LifecycleStatusTagCellRenderer = <
  T extends { status: LifecycleNavigationDto },
>(
  props: CustomCellRendererProps<T>,
) => {
  const { data } = props
  if (!data?.status) return null
  return <LifecycleStatusTag status={data.status} />
}

export const DependencyHealthCellRenderer = <
  T extends { health: { id: DependencyHealth; name: string } },
>(
  props: CustomCellRendererProps<T>,
) => {
  const { value, data } = props
  if (!data?.health) return null
  return <DependencyHealthTag name={value} health={data.health.id} />
}

export const MarkdownCellRenderer = (props: CustomCellRendererProps) => {
  const { value } = props
  if (!value) return null
  return (
    <div className="grid-react-markdown">
      <MarkdownRenderer markdown={value} />
    </div>
  )
}

export interface TeamNameLinkColumn extends NavigationDto {
  type?: string
}

export const NestedTeamNameLinkCellRenderer = <
  T extends { team: TeamNameLinkColumn | null },
>(
  props: CustomCellRendererProps<T>,
) => {
  const { data } = props
  if (!data?.team) return null
  const teamLink =
    data.team.type === 'Team'
      ? `/organizations/teams/${data.team.key}`
      : `/organizations/team-of-teams/${data.team.key}`
  return <Link href={teamLink}>{data.team.name}</Link>
}

export const NestedTeamOfTeamsNameLinkCellRenderer = <
  T extends { teamOfTeams: TeamNameLinkColumn | null },
>(
  props: CustomCellRendererProps<T>,
) => {
  const { data } = props
  if (!data?.teamOfTeams) return null
  const teamLink =
    data.teamOfTeams.type === 'Team'
      ? `/organizations/teams/${data.teamOfTeams.key}`
      : `/organizations/team-of-teams/${data.teamOfTeams.key}`
  return <Link href={teamLink}>{data.teamOfTeams.name}</Link>
}

export const TeamNameLinkCellRenderer = (
  props: CustomCellRendererProps<TeamNameLinkColumn>,
) => {
  const { data } = props
  if (!data) return null
  const teamLink =
    data.type === 'Team'
      ? `/organizations/teams/${data.key}`
      : `/organizations/team-of-teams/${data.key}`
  return <Link href={teamLink}>{data.name}</Link>
}

export const PlanningIntervalObjectiveLinkCellRenderer = (
  props: CustomCellRendererProps<PlanningIntervalObjectiveListDto>,
) => {
  const { data } = props
  if (!data) return null
  return (
    <Link
      href={`/planning/planning-intervals/${data.planningInterval?.key}/objectives/${data.key}`}
    >
      {data.name}
    </Link>
  )
}

export const NestedPlanningIntervalLinkCellRenderer = <
  T extends { planningInterval: NavigationDto | null },
>(
  props: CustomCellRendererProps<T>,
) => {
  const { data } = props
  if (!data?.planningInterval) return null
  return (
    <Link href={`/planning/planning-intervals/${data.planningInterval.key}`}>
      {data.planningInterval.name}
    </Link>
  )
}

export const PlanningIntervalLinkCellRenderer = (
  props: CustomCellRendererProps<NavigationDto>,
) => {
  const { data } = props
  if (!data) return null
  return (
    <Link href={`/planning/planning-intervals/${data.key}`}>{data.name}</Link>
  )
}

export const PortfolioLinkCellRenderer = (
  props: CustomCellRendererProps<NavigationDto>,
) => {
  const { data } = props
  if (!data) return null
  return <Link href={`/ppm/portfolios/${data.key}`}>{data.name}</Link>
}

export const ProgramLinkCellRenderer = <
  T extends NavigationDto | { program: NavigationDto | null },
>(
  props: CustomCellRendererProps<T>,
) => {
  const { data } = props
  if (!data) return null

  // Handle both direct NavigationDto and nested program cases
  const programData: NavigationDto | null =
    'program' in data ? data.program : data
  if (!programData) return null

  return (
    <Link href={`/ppm/programs/${programData.key}`}>{programData.name}</Link>
  )
}

export const ProjectLinkCellRenderer = <
  T extends NavigationDto | { project: NavigationDto | null },
>(
  props: CustomCellRendererProps<T>,
) => {
  const { data } = props
  if (!data) return null

  // Handle both direct NavigationDto and nested project cases
  const projectData: NavigationDto | null =
    'project' in data ? data.project : data
  if (!projectData) return null

  return (
    <Link href={`/ppm/projects/${projectData.key}`}>{projectData.name}</Link>
  )
}

export interface RowMenuCellRendererProps extends CustomCellRendererProps {
  menuItems: ItemType[]
}
export const RowMenuCellRenderer = (props: RowMenuCellRendererProps) => {
  if (props.menuItems.length === 0) return null

  return (
    <Dropdown menu={{ items: props.menuItems }} trigger={['click']}>
      <Button type="text" size="small" icon={<MoreOutlined />} />
    </Dropdown>
  )
}

export const WorkspaceLinkCellRenderer = (
  props: CustomCellRendererProps<NavigationDto>,
) => {
  const { data } = props
  if (!data) return null
  return <Link href={`/work/workspaces/${data.key}`}>{data.name}</Link>
}

export const NestedWorkSprintLinkCellRenderer = <
  T extends { sprint: WorkIterationNavigationDto | null },
>(
  props: CustomCellRendererProps<T> & { showTeamCode?: boolean },
) => {
  const { data, showTeamCode = true } = props
  if (!data?.sprint) return null

  const displayText =
    showTeamCode && data.sprint.team?.code
      ? `${data.sprint.name} (${data.sprint.team.code})`
      : data.sprint.name

  return (
    <Link href={`/planning/sprints/${data.sprint.key}`}>{displayText}</Link>
  )
}

export const WorkSprintLinkCellRenderer = (
  props: CustomCellRendererProps<WorkIterationNavigationDto> & {
    showTeamCode?: boolean
  },
) => {
  const { data, showTeamCode = true } = props
  if (!data) return null

  const displayText =
    showTeamCode && data.team?.code
      ? `${data.name} (${data.team.code})`
      : data.name

  return <Link href={`/planning/sprints/${data.key}`}>{displayText}</Link>
}

// Work Item Cell Renderers
export interface WorkItemLinkColumn {
  key: string
  workspace: { key: string }
  externalViewWorkItemUrl?: string | null
}

export const WorkItemLinkCellRenderer = <T extends WorkItemLinkColumn>(
  props: CustomCellRendererProps<T>,
) => {
  const { value, data } = props
  if (!data) return null

  return (
    <>
      <Link
        href={`/work/workspaces/${data.workspace.key}/work-items/${data.key}`}
        prefetch={false}
      >
        {value}
      </Link>

      {data.externalViewWorkItemUrl && (
        <Link
          href={data.externalViewWorkItemUrl}
          target="_blank"
          title="Open in external system"
          style={{ marginLeft: '5px' }}
        >
          <ExportOutlined style={{ width: '10px' }} />
        </Link>
      )}
    </>
  )
}

export const ParentWorkItemLinkCellRenderer = <
  T extends {
    parent: {
      key: string
      workspaceKey: string
      externalViewWorkItemUrl?: string | null
    } | null
  },
>(
  props: CustomCellRendererProps<T>,
) => {
  const { value, data } = props
  if (!data?.parent) return null

  return (
    <>
      <Link
        href={`/work/workspaces/${data.parent.workspaceKey}/work-items/${data.parent.key}`}
        prefetch={false}
      >
        {value}
      </Link>
      {data.parent.externalViewWorkItemUrl && (
        <Link
          href={data.parent.externalViewWorkItemUrl}
          target="_blank"
          title="Open in external system"
          style={{ marginLeft: '5px' }}
        >
          <ExportOutlined style={{ width: '10px' }} />
        </Link>
      )}
    </>
  )
}

export const AssignedToLinkCellRenderer = <
  T extends { assignedTo: { key: string; name?: string } | null },
>(
  props: CustomCellRendererProps<T>,
) => {
  const { value, data } = props
  if (!data?.assignedTo) return null

  return (
    <Link
      href={`/organizations/employees/${data.assignedTo.key}`}
      prefetch={false}
    >
      {value}
    </Link>
  )
}

export const UserLinkCellRenderer = (
  props: CustomCellRendererProps<UserDetailsDto>,
) => {
  if (!props.data) return null
  return (
    <Link href={`/settings/user-management/users/${props.data.id}`}>
      {props.data.userName}
    </Link>
  )
}

// Helper utility functions for rendering links (can be used outside of cell renderers)
export const renderWorkItemLinkHelper = (
  workItem:
    | {
        key: string
        workspaceKey: string
        externalViewWorkItemUrl?: string | null
      }
    | null
    | undefined,
) => {
  if (!workItem) return null
  return (
    <>
      <Link
        href={`/work/workspaces/${workItem.workspaceKey}/work-items/${workItem.key}`}
        prefetch={false}
      >
        {workItem.key}
      </Link>
      {workItem.externalViewWorkItemUrl && (
        <Link
          href={workItem.externalViewWorkItemUrl}
          target="_blank"
          title="Open in external system"
          style={{ marginLeft: '5px' }}
        >
          <ExportOutlined style={{ width: '10px' }} />
        </Link>
      )}
    </>
  )
}

export const renderTeamLinkHelper = (
  team:
    | { key: number | string; name: string; type?: string }
    | null
    | undefined,
) => {
  if (!team) return null
  const teamLink =
    team.type === 'Team'
      ? `/organizations/teams/${team.key}`
      : `/organizations/team-of-teams/${team.key}`
  return <Link href={teamLink}>{team.name}</Link>
}

export const renderSprintLinkHelper = (
  sprint: { key: number | string; name: string } | null | undefined,
) => {
  if (!sprint) return null
  return <Link href={`/planning/sprints/${sprint.key}`}>{sprint.name}</Link>
}

export const DateTimeCellRenderer = (props: CustomCellRendererProps) => {
  const { value } = props
  if (!value) return ''
  return new Date(value).toLocaleString(undefined, {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  })
}

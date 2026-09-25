'use client'

import { WaydEmpty } from '@/src/components/common'
import {
  WaydTimeline,
  type GroupRenderProps,
  type TimelineGroup,
  type TimelineItem,
} from '@/src/components/common/timeline'
import { ProjectListDto, ProjectStageListDto } from '@/src/services/wayd-api'
import { Flex, theme } from 'antd'
import dayjs, { Dayjs } from 'dayjs'
import { FC } from 'react'
import { healthName, PlanSummaries, ProjectGroup } from './dashboard-model'
import styles from '../projects-dashboard.module.css'

export interface ProjectsDashboardTimelineProps {
  groups: ProjectGroup[]
  planSummaries: PlanSummaries
  employeeId: string | null
  selectedProjectKey: string | null
  onSelectProject: (key: string) => void
  isLoading: boolean
  today: Dayjs
  height?: number
}

/** What a timeline row or bar stands for, so a click can find its project. */
interface RowPayload {
  kind: 'group' | 'project'
  project?: ProjectListDto
  count?: number
}

interface BarPayload {
  projectKey: string
}

type Token = ReturnType<typeof theme.useToken>['token']

const ms = (d: Date) => dayjs(d).valueOf()
const fmt = (d: Date) => dayjs(d).format('MMM D, YYYY')

const healthColor = (project: ProjectListDto, token: Token): string => {
  switch (project.healthCheck?.status.name) {
    case 'Healthy':
      return token.colorSuccess
    case 'At Risk':
      return token.colorWarning
    case 'Unhealthy':
      return token.colorError
    default:
      return token.colorTextDisabled
  }
}

const stageColor = (stage: ProjectStageListDto, token: Token): string => {
  switch (stage.status.name) {
    case 'Completed':
      return token.colorPrimaryBorder
    case 'In Progress':
      return token.colorPrimary
    case 'Canceled':
      return token.colorTextQuaternary
    default:
      return token.colorFillSecondary
  }
}

export interface TimelineModel {
  items: TimelineItem<BarPayload>[]
  groups: TimelineGroup<RowPayload>[]
  windowStart: number
  windowEnd: number
  minDate: number
  maxDate: number
  /** Projects with neither a dated bar nor a dated stage: their rows are empty. */
  undatedCount: number
}

/**
 * One row per project under a heading row per dashboard group. A row holds
 * the project's own bar, coloured by health, and one bar per dated stage,
 * coloured by stage status. The timeline packs overlapping bars into lanes,
 * so two stages running at once sit one under the other rather than one
 * hiding the other; the project bar starts first and sorts first, so it
 * takes the top lane.
 */
export const buildTimelineModel = (
  groups: ProjectGroup[],
  today: Dayjs,
  token: Token,
): TimelineModel => {
  const items: TimelineItem<BarPayload>[] = []
  const rows: TimelineGroup<RowPayload>[] = []
  let minMs = Number.POSITIVE_INFINITY
  let maxMs = Number.NEGATIVE_INFINITY
  let undatedCount = 0

  const track = (start: number, end: number) => {
    if (start < minMs) minMs = start
    if (end > maxMs) maxMs = end
  }

  groups.forEach((group, groupIndex) => {
    const groupRowId = `group:${group.key}`
    rows.push({
      id: groupRowId,
      label: group.name,
      order: groupIndex,
      data: { kind: 'group', count: group.projects.length },
    })

    group.projects.forEach((project, projectIndex) => {
      const rowId = `project:${project.id}`
      rows.push({
        id: rowId,
        parentId: groupRowId,
        label: `${project.key} · ${project.name}`,
        order: projectIndex,
        data: { kind: 'project', project },
      })

      let dated = false
      if (project.start && project.end) {
        const start = ms(project.start)
        const end = ms(project.end)
        track(start, end)
        dated = true
        items.push({
          id: `project:${project.id}`,
          kind: 'range',
          start,
          end,
          label: project.name,
          tooltip: `${project.key} · ${project.name}\n${healthName(project)} · ${project.status.name}\n${fmt(project.start)} – ${fmt(project.end)}`,
          color: healthColor(project, token),
          groupId: rowId,
          // Sorts ahead of any stage starting the same day, so the project bar
          // takes the top lane of its row.
          order: -1,
          data: { projectKey: project.key },
        })
      }

      for (const stage of project.stages ?? []) {
        if (!stage.start || !stage.end) continue
        const start = ms(stage.start)
        const end = ms(stage.end)
        track(start, end)
        dated = true
        const progress =
          stage.status.name === 'In Progress' ? ` · ${stage.progress}%` : ''
        items.push({
          id: `stage:${stage.id}`,
          kind: 'range',
          start,
          end,
          label: stage.name,
          tooltip: `${stage.name} · ${stage.status.name}${progress}\n${fmt(stage.start)} – ${fmt(stage.end)}`,
          color: stageColor(stage, token),
          groupId: rowId,
          order: stage.order,
          data: { projectKey: project.key },
        })
      }

      if (!dated) undatedCount++
    })
  })

  const hasDates = Number.isFinite(minMs)
  const anchor = today.valueOf()

  return {
    items,
    groups: rows,
    windowStart: today.subtract(3, 'month').valueOf(),
    windowEnd: today.add(9, 'month').valueOf(),
    minDate: dayjs(hasDates ? minMs : anchor)
      .subtract(1, 'month')
      .valueOf(),
    maxDate: dayjs(hasDates ? maxMs : anchor)
      .add(1, 'month')
      .valueOf(),
    undatedCount,
  }
}

const RowLabel: FC<GroupRenderProps<RowPayload>> = ({ group }) => {
  const payload = group.data
  if (payload?.kind === 'project' && payload.project) {
    return (
      <span className={styles.timelineProjectLabel}>
        <span className={styles.key}>{payload.project.key}</span>{' '}
        {payload.project.name}
      </span>
    )
  }
  const count = payload?.count ?? 0
  return (
    <span className={styles.timelineGroupLabel}>
      {group.label}
      <span className={styles.groupMeta}>
        {' '}
        · {count} {count === 1 ? 'project' : 'projects'}
      </span>
    </span>
  )
}

const Legend: FC<{ token: Token; undatedCount: number }> = ({
  token,
  undatedCount,
}) => {
  const swatch = (color: string, label: string) => (
    <Flex key={label} align="center" gap={5}>
      <span className={styles.legendSwatch} style={{ background: color }} />
      <span>{label}</span>
    </Flex>
  )
  return (
    <Flex align="center" gap={16} wrap className={styles.timelineLegend}>
      <span className={styles.scopeLabel}>Project</span>
      {swatch(token.colorSuccess, 'Healthy')}
      {swatch(token.colorWarning, 'At risk')}
      {swatch(token.colorError, 'Unhealthy')}
      {swatch(token.colorTextDisabled, 'Not reported')}
      <span className={styles.scopeDivider} />
      <span className={styles.scopeLabel}>Stage</span>
      {swatch(token.colorPrimaryBorder, 'Completed')}
      {swatch(token.colorPrimary, 'In progress')}
      {swatch(token.colorFillSecondary, 'Not started')}
      {undatedCount > 0 && (
        <span className={styles.groupMeta} style={{ marginLeft: 'auto' }}>
          {undatedCount} {undatedCount === 1 ? 'project has' : 'projects have'}{' '}
          no dates to draw
        </span>
      )}
    </Flex>
  )
}

/**
 * The Timeline view: the same groups as the list, drawn on a time axis with
 * each project's lifecycle stages on their own dates. Clicking any bar opens
 * that project.
 */
const ProjectsDashboardTimeline: FC<ProjectsDashboardTimelineProps> = ({
  groups,
  onSelectProject,
  isLoading,
  today,
  height,
}) => {
  const { token } = theme.useToken()

  if (!isLoading && groups.length === 0) {
    return (
      <div className={`${styles.list} ${styles.emptyState}`}>
        <WaydEmpty message="No projects match the current scope and filters." />
      </div>
    )
  }

  const model = buildTimelineModel(isLoading ? [] : groups, today, token)

  return (
    <WaydTimeline<BarPayload, RowPayload>
      variant="timeline"
      items={model.items}
      groups={model.groups}
      windowStart={model.windowStart}
      windowEnd={model.windowEnd}
      minDate={model.minDate}
      maxDate={model.maxDate}
      storageKey="projects-dashboard"
      height={height}
      laneHeight={22}
      groupColumnWidth={280}
      editable={false}
      isLoading={isLoading}
      allowFullScreen
      allowSaveAsImage
      saveImageFileName="Projects Dashboard Timeline"
      groupRenderer={RowLabel}
      onItemClick={(item) => {
        if (item.data) onSelectProject(item.data.projectKey)
      }}
      footerSlot={<Legend token={token} undatedCount={model.undatedCount} />}
    />
  )
}

export default ProjectsDashboardTimeline

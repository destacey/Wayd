'use client'

import { WaydEmpty } from '@/src/components/common'
import {
  WaydTimeline,
  type GroupRenderProps,
  type TimelineGroup,
  type TimelineItem,
} from '@/src/components/common/timeline'
import ProjectHealthCheckTag from '@/src/app/ppm/projects/_components/project-health-check-tag'
import { LifecycleCategory } from '@/src/components/types'
import { ProjectListDto, ProjectStageListDto } from '@/src/services/wayd-api'
import {
  getLifecycleCategoryColor,
  getLifecycleCategoryColorFromStatus,
} from '@/src/utils'
import type { SemanticColorTokens } from '@/src/utils/color-helper'
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

type Token = SemanticColorTokens

const ms = (d: Date) => dayjs(d).valueOf()
const fmt = (d: Date) => dayjs(d).format('MMM D, YYYY')

/**
 * A stage's status as a lifecycle category, so stage bars take the same
 * colours as project bars here and on the portfolio timeline.
 */
const stageCategory = (stage: ProjectStageListDto): LifecycleCategory => {
  switch (stage.status.name) {
    case 'Completed':
      return LifecycleCategory.Completed
    case 'In Progress':
      return LifecycleCategory.Active
    case 'Canceled':
      return LifecycleCategory.Canceled
    default:
      return LifecycleCategory.NotStarted
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
 * the project's own bar and one bar per dated stage, both coloured by status
 * the way the portfolio timeline colours its projects; health is the flag on
 * the row label, not the bar. The timeline packs overlapping bars into lanes,
 * so two stages running at once sit one under the other rather than one
 * hiding the other; the project bar is pinned to the top lane.
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
        label: project.name,
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
          color: getLifecycleCategoryColorFromStatus(project.status, token),
          groupId: rowId,
          // The project's own span always heads its row; a stage that starts
          // before the project would otherwise take the top lane.
          pinToTop: true,
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
          color: getLifecycleCategoryColor(stageCategory(stage), token),
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
        <ProjectHealthCheckTag
          healthCheck={payload.project.healthCheck}
          projectId={payload.project.id}
          variant="flag"
        />{' '}
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
      <span className={styles.scopeLabel}>Status</span>
      {swatch(
        getLifecycleCategoryColor(LifecycleCategory.NotStarted, token),
        'Not started',
      )}
      {swatch(
        getLifecycleCategoryColor(LifecycleCategory.Active, token),
        'Active',
      )}
      {swatch(
        getLifecycleCategoryColor(LifecycleCategory.Completed, token),
        'Completed',
      )}
      {swatch(
        getLifecycleCategoryColor(LifecycleCategory.Canceled, token),
        'Canceled',
      )}
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

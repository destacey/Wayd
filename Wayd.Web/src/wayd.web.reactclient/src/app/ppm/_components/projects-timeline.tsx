'use client'

import { FC, ReactNode, useState } from 'react'
import dayjs from 'dayjs'
import { theme } from 'antd'
import { ProjectListDto } from '@/src/services/wayd-api'
import { WaydTimeline } from '@/src/components/common/timeline'
import type { TimelineItem, TimelineGroup } from '@/src/components/common/timeline'
import { getLifecyclePhaseColorFromStatus } from '@/src/utils'
import { ProjectDrawer } from '.'

const ms = (d: dayjs.ConfigType) => dayjs(d).valueOf()

interface ProjectPayload {
  dto: ProjectListDto
}

export interface ProjectsTimelineProps {
  projects: ProjectListDto[]
  isLoading: boolean
  refetch: () => void
  viewSelector?: ReactNode
  groupByProgram?: boolean
  onRefresh?: () => void
}

function mapProjects(
  projects: ProjectListDto[],
  groupByProgram: boolean,
  token: ReturnType<typeof theme.useToken>['token'],
): {
  items: TimelineItem<ProjectPayload>[]
  groups: TimelineGroup[]
  windowStart: number
  windowEnd: number
  minDate: number
  maxDate: number
} {
  const dated = projects.filter((p) => p.start && p.end)

  let minMs = dated.length > 0 ? ms(dated[0].start!) : dayjs().valueOf()
  let maxMs = dated.length > 0 ? ms(dated[0].end!) : dayjs().valueOf()

  const items: TimelineItem<ProjectPayload>[] = dated.map((p, idx) => {
    const start = ms(p.start!)
    const end = ms(p.end!)
    if (start < minMs) minMs = start
    if (end > maxMs) maxMs = end
    return {
      id: String(p.id),
      kind: 'range',
      label: p.name ?? '',
      color: getLifecyclePhaseColorFromStatus(p.status, token),
      start,
      end,
      groupId: groupByProgram ? (p.program?.name ?? 'No Program') : undefined,
      order: idx,
      data: { dto: p },
    }
  })

  // Build groups: alphabetical, 'No Program' last — skip if only one group and
  // it would be 'No Program' (matches legacy: returns [] when all ungrouped).
  let groups: TimelineGroup[] = []
  if (groupByProgram) {
    const seen = new Map<string, TimelineGroup>()
    for (const p of dated) {
      const name = p.program?.name ?? 'No Program'
      if (!seen.has(name)) {
        seen.set(name, { id: name, label: name })
      }
    }
    const all = [...seen.values()]
    if (!(all.length === 1 && all[0].id === 'No Program')) {
      groups = all.sort((a, b) => {
        if (a.id === 'No Program') return 1
        if (b.id === 'No Program') return -1
        return a.id.localeCompare(b.id)
      })
    }
  }

  const windowStart = dayjs().subtract(6, 'months').valueOf()
  const windowEnd = dayjs().add(6, 'months').valueOf()
  const minDate = dayjs(minMs).subtract(1, 'month').valueOf()
  const maxDate = dayjs(maxMs).add(1, 'month').valueOf()

  return { items, groups, windowStart, windowEnd, minDate, maxDate }
}

const ProjectsTimeline: FC<ProjectsTimelineProps> = ({
  projects,
  isLoading,
  viewSelector,
  groupByProgram = false,
  onRefresh,
}) => {
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [selectedKey, setSelectedKey] = useState<string | null>(null)
  const { token } = theme.useToken()

  const { items, groups, windowStart, windowEnd, minDate, maxDate } = mapProjects(
    isLoading ? [] : projects,
    groupByProgram,
    token,
  )

  const noDatesCount = projects.filter((p) => !p.start || !p.end).length

  return (
    <>
      <WaydTimeline<ProjectPayload>
        variant="timeline"
        items={items}
        groups={groups.length > 0 ? groups : undefined}
        windowStart={windowStart}
        windowEnd={windowEnd}
        minDate={minDate}
        maxDate={maxDate}
        storageKey="ppm-projects"
        height={650}
        editable={false}
        isLoading={isLoading}
        allowFullScreen
        allowSaveAsImage
        saveImageFileName="Projects Timeline"
        onRefresh={onRefresh}
        toolbarRightSlot={viewSelector}
        onItemClick={(item) => {
          if (!item.data) return
          setSelectedKey(item.data.dto.key)
          setDrawerOpen(true)
        }}
        emptyMessage={
          noDatesCount > 0
            ? `${noDatesCount} ${noDatesCount === 1 ? 'project is' : 'projects are'} not shown due to missing dates.`
            : undefined
        }
      />
      {selectedKey && (
        <ProjectDrawer
          projectKey={selectedKey}
          drawerOpen={drawerOpen}
          onDrawerClose={() => {
            setDrawerOpen(false)
            setSelectedKey(null)
          }}
        />
      )}
    </>
  )
}

export default ProjectsTimeline

'use client'

// planning-interval-objectives-timeline.tsx — adapter feeding PI objectives
// data into WaydTimeline.
//
// iteration schedule -> kind 'background'  (scoped to a synthetic root band when
//                                           groups are shown, so the label has headroom)
// objective          -> kind 'range'        (one bar per objective, grouped by team)

import { FC, ReactNode } from 'react'
import dayjs from 'dayjs'
import { useRouter } from 'next/navigation'
import {
  PlanningIntervalCalendarDto,
  PlanningIntervalObjectiveListDto,
} from '@/src/services/wayd-api'
import { WaydTimeline } from '@/src/components/common/timeline'
import type { TimelineItem, TimelineGroup } from '@/src/components/common/timeline'


const ms = (d: dayjs.ConfigType) => dayjs(d).valueOf()

interface PiObjectivesPayload {
  dto: PlanningIntervalObjectiveListDto
}

export interface PlanningIntervalObjectivesTimelineProps {
  objectivesData: PlanningIntervalObjectiveListDto[]
  planningIntervalCalendar: PlanningIntervalCalendarDto
  enableGroups?: boolean
  teamNames?: string[]
  viewSelector?: ReactNode
  onObjectiveClick?: (objectiveKey: number) => void
  onRefresh?: () => void
}

function mapObjectives(
  objectivesData: PlanningIntervalObjectiveListDto[],
  planningIntervalCalendar: PlanningIntervalCalendarDto,
  enableGroups: boolean,
  teamNames: string[] | undefined,
): {
  items: TimelineItem<PiObjectivesPayload>[]
  groups: TimelineGroup[]
} {
  // Synthetic root group id — hosts iteration backgrounds when team groups are
  // shown, so the band label gets a dedicated headroom row above the team rows
  // rather than floating over the first team's bars.
  const ROOT_GROUP_ID = '__pi_root__'

  // Background items: one per iteration schedule. When groups are shown they are
  // scoped to the root band; otherwise chart-wide (no groupId).
  const backgrounds: TimelineItem<PiObjectivesPayload>[] =
    planningIntervalCalendar.iterationSchedules?.map(
      (iter): TimelineItem<PiObjectivesPayload> => ({
        id: `iter-${iter.key}`,
        kind: 'background',
        label: iter.name ?? '',
        // Iteration end is inclusive in the DTO; add 1 day minus 1 second to
        // match the legacy component's rendering (fills to end of that day).
        start: ms(iter.start),
        end: ms(dayjs(iter.end as unknown as string).add(1, 'day').subtract(1, 'second')),
        groupId: enableGroups ? ROOT_GROUP_ID : undefined,
      }),
    ) ?? []

  const active = (objectivesData ?? []).filter(
    (obj) => obj.status?.name !== 'Canceled',
  )

  // Determine group list: either from teamNames prop or derived from objectives.
  let groupIds: string[]
  if (enableGroups) {
    if (teamNames && teamNames.length > 0) {
      groupIds = [...teamNames].sort((a, b) => a.localeCompare(b))
    } else {
      groupIds = active
        .reduce<string[]>((acc, obj) => {
          const name = obj.team?.name
          if (name && !acc.includes(name)) acc.push(name)
          return acc
        }, [])
        .sort((a, b) => a.localeCompare(b))
    }
  } else {
    groupIds = []
  }

  const teamGroups: TimelineGroup[] = groupIds.map((name, idx) => ({
    id: name,
    label: name,
    order: idx,
  }))

  // Prepend a blank root band when there are iteration backgrounds to host —
  // this gives the band labels headroom above the team rows.
  const groups: TimelineGroup[] =
    enableGroups && backgrounds.length > 0
      ? [{ id: ROOT_GROUP_ID, label: '', order: -Infinity }, ...teamGroups]
      : teamGroups

  const bars: TimelineItem<PiObjectivesPayload>[] = active.map(
    (obj, idx): TimelineItem<PiObjectivesPayload> => ({
      id: String(obj.key),
      kind: 'range',
      label: `${obj.key} - ${obj.name ?? ''}`,
      start: ms(obj.startDate ?? planningIntervalCalendar.start),
      end: ms(obj.targetDate ?? planningIntervalCalendar.end),
      groupId: enableGroups ? (obj.team?.name ?? undefined) : undefined,
      order: obj.order ?? idx,
      progress: obj.progress,
      data: { dto: obj },
    }),
  )

  return { items: [...backgrounds, ...bars], groups }
}

const PlanningIntervalObjectivesTimeline: FC<
  PlanningIntervalObjectivesTimelineProps
> = ({
  objectivesData,
  planningIntervalCalendar,
  enableGroups = false,
  teamNames,
  viewSelector,
  onObjectiveClick,
  onRefresh,
}) => {
  const router = useRouter()

  if (!planningIntervalCalendar) return null

  const { items, groups } = mapObjectives(
    objectivesData,
    planningIntervalCalendar,
    enableGroups,
    teamNames,
  )

  const windowStart = ms(planningIntervalCalendar.start)
  const windowEnd = ms(planningIntervalCalendar.end)
  const piKey = planningIntervalCalendar.key

  return (
    <WaydTimeline<PiObjectivesPayload>
      variant="timeline"
      items={items}
      groups={groups.length > 0 ? groups : undefined}
      windowStart={windowStart}
      windowEnd={windowEnd}
      minDate={windowStart}
      maxDate={windowEnd}
      storageKey={`pi-objectives-${piKey}`}
      height={650}
      // Read-only: no drag/resize/progress editing.
      editable={false}
      allowFullScreen
      allowSaveAsImage
      saveImageFileName={`PI ${planningIntervalCalendar.name} Objectives`}
      onRefresh={onRefresh}
      toolbarRightSlot={viewSelector}
      onItemClick={(item) => {
        if (!item.data) return
        const { dto } = item.data
        if (onObjectiveClick) {
          onObjectiveClick(dto.key)
        } else {
          router.push(
            `/planning/planning-intervals/${dto.planningInterval?.key}/objectives/${dto.key}`,
          )
        }
      }}
    />
  )
}

export default PlanningIntervalObjectivesTimeline

'use client'

// roadmap-timeline-v2.tsx — adapter that feeds roadmap data into the new
// in-house WaydTimeline2 component. Rendered in place of the legacy vis-timeline
// (roadmaps-timeline.tsx) when the "new-timeline-ui" feature flag is enabled;
// RoadmapViewManager picks between the two. Maps the roadmap item tree to the
// component's TimelineItem/TimelineGroup shape (epoch-ms bounds).
//
// activity  -> kind 'range'      (and a GROUP, so its children can nest)
// milestone -> kind 'milestone'
// timebox   -> kind 'background' (chart-wide band for now)

import { FC, ReactNode, useState } from 'react'
import dayjs from 'dayjs'
import {
  RoadmapActivityListDto,
  RoadmapDetailsDto,
  RoadmapItemListDto,
  RoadmapMilestoneListDto,
  RoadmapTimeboxListDto,
} from '@/src/services/wayd-api'
import { WaydTimeline2 } from '@/src/components/common/timeline2'
import type {
  TimelineItem,
  TimelineGroup,
  ItemDateChange,
} from '@/src/components/common/timeline2'
import { useUpdateRoadmapItemDatesMutation } from '@/src/store/features/planning/roadmaps-api'
import { useMessage } from '@/src/components/contexts/messaging'
import { isApiError, type ApiError } from '@/src/utils'
import RoadmapColorLegend from './roadmap-color-legend'

export interface RoadmapTimelineV2Props {
  roadmap: RoadmapDetailsDto
  roadmapItems: RoadmapItemListDto[]
  isRoadmapItemsLoading: boolean
  refreshRoadmapItems: () => void
  viewSelector?: ReactNode
  openRoadmapItemDrawer: (itemId: string) => void
  isRoadmapManager: boolean
}

enum RoadmapItemType {
  Activity = 'activity',
  Milestone = 'milestone',
  Timebox = 'timebox',
}

const ms = (d: unknown) => dayjs(d as string).valueOf()

interface RoadmapPayload {
  dto: RoadmapItemListDto
  color?: string
  openDrawer: (id: string) => void
}

// Walk the roadmap item tree -> flat TimelineItems + Groups for WaydTimeline2.
// EVERY activity is emitted as BOTH a group (potential left-column row) AND a
// range item assigned to its own group. The drill level (resolved inside
// WaydTimeline2) then decides which activities stay groups vs. demote to bars in
// an ancestor row — so the adapter just exposes the full tree, no level logic.
// Synthetic group for the roadmap-root band — holds root-level timeboxes /
// milestones (attached to the roadmap itself, not an activity), like the legacy
// timeline's blank first row.
const ROOT_GROUP_ID = '__roadmap_root__'

function mapRoadmap(
  items: RoadmapItemListDto[],
  openDrawer: (id: string) => void,
  // The roadmap's default color, applied at display time to activities that have
  // no color of their own. Undefined when no default is configured (falls through
  // to the timeline's theme color).
  defaultActivityColor: string | undefined,
): { items: TimelineItem<RoadmapPayload>[]; groups: TimelineGroup[] } {
  const out: TimelineItem<RoadmapPayload>[] = []
  const groups: TimelineGroup[] = []
  let hasRootDecoration = false

  // `treeLevel` is 1-based: top-level activities = 1, their children = 2, etc.
  // (matches the legacy timeline's treeLevel). Milestones/timeboxes take their
  // parent activity's level + 1. A root-level decoration (depth 1, no parent
  // activity) is assigned to the synthetic root group so it gets a top band.
  const walk = (
    list: RoadmapItemListDto[],
    order: number,
    treeLevel: number,
  ) => {
    list.forEach((item, index) => {
      const id = String(item.id)
      const parentId = item.parent?.id ? String(item.parent.id) : undefined
      // Decorations (timebox/milestone) at the top level have no parent
      // ACTIVITY group — route them to the synthetic root band.
      const isActivity = item.$type === RoadmapItemType.Activity
      const decorationGroupId =
        !isActivity && treeLevel === 1 ? ROOT_GROUP_ID : parentId
      if (!isActivity && treeLevel === 1) hasRootDecoration = true
      const payload: RoadmapPayload = {
        dto: item,
        color: item.color,
        openDrawer,
      }

      switch (item.$type) {
        case RoadmapItemType.Activity: {
          const a = item as RoadmapActivityListDto
          // Activity = a group (with its real parent for hierarchy) AND a range
          // item in that group. The drill level decides which role it plays.
          groups.push({
            id,
            parentId,
            treeLevel,
            label: a.name,
            order: a.order ?? order + index,
          })
          out.push({
            id,
            kind: 'range',
            label: a.name,
            // Activities with no color of their own fall back to the roadmap's
            // configured default color (display-time only; nothing is stored).
            color: a.color ?? defaultActivityColor,
            start: ms(a.start),
            end: ms(a.end),
            groupId: id,
            treeLevel,
            order: a.order ?? order + index,
            data: payload,
          })
          if (a.children?.length) walk(a.children, 0, treeLevel + 1)
          break
        }
        case RoadmapItemType.Milestone: {
          const m = item as RoadmapMilestoneListDto
          out.push({
            id,
            kind: 'milestone',
            label: m.name,
            color: m.color,
            start: ms(m.date),
            end: ms(m.date),
            groupId: decorationGroupId,
            treeLevel,
            order: order + index,
            data: payload,
          })
          break
        }
        case RoadmapItemType.Timebox: {
          const t = item as RoadmapTimeboxListDto
          out.push({
            id,
            kind: 'background',
            label: t.name,
            color: t.color,
            start: ms(t.start),
            end: ms(t.end),
            groupId: decorationGroupId,
            treeLevel,
            order: order + index,
            data: payload,
          })
          break
        }
      }
    })
  }

  walk(items, 0, 1)

  // Prepend the synthetic root band group (blank label, sorts first) when there
  // are root-level decorations to host. treeLevel 0 keeps it always visible.
  if (hasRootDecoration) {
    groups.unshift({
      id: ROOT_GROUP_ID,
      treeLevel: 0,
      label: '',
      order: -Infinity,
    })
  }

  return { items: out, groups }
}

const RoadmapTimelineV2: FC<RoadmapTimelineV2Props> = (props) => {
  const messageApi = useMessage()
  const [updateRoadmapItemDates] = useUpdateRoadmapItemDatesMutation()
  const [updatingId, setUpdatingId] = useState<string | null>(null)

  const defaultActivityColor = props.roadmap.colors.find((c) => c.isDefault)
    ?.color

  const { items, groups } = mapRoadmap(
    props.roadmapItems,
    props.openRoadmapItemDrawer,
    defaultActivityColor,
  )

  const windowStart = ms(props.roadmap.start)
  const windowEnd = ms(props.roadmap.end)

  const ONE_YEAR_MS = 365 * 24 * 60 * 60 * 1000
  const itemStarts = items.map((i) => i.start)
  const itemEnds = items.map((i) => i.end)
  const earliestItem = itemStarts.length ? Math.min(...itemStarts) : windowStart
  const latestItem = itemEnds.length ? Math.max(...itemEnds) : windowEnd
  const minDate = earliestItem - ONE_YEAR_MS
  const maxDate = latestItem + ONE_YEAR_MS

  const onItemDateChange = async (change: ItemDateChange) => {
    const source = items.find((i) => i.id === change.id)
    const dto = source?.data?.dto
    if (!dto) return

    setUpdatingId(change.id)
    try {
      const response = await updateRoadmapItemDates({
        $type: dto.$type,
        roadmapId: dto.roadmapId,
        itemId: change.id,
        start: dayjs(change.start).format('YYYY-MM-DD') as unknown as Date,
        end: dayjs(change.end).format('YYYY-MM-DD') as unknown as Date,
      })
      if ('error' in response && response.error) throw response.error
    } catch (error) {
      const apiError: ApiError = isApiError(error) ? error : {}
      messageApi.error(
        apiError.detail ??
          'An error occurred while updating the roadmap item. Please try again.',
      )
    } finally {
      setUpdatingId(null)
    }
  }

  return (
    <>
      <WaydTimeline2<RoadmapPayload>
        variant="timeline"
        items={items}
        groups={groups}
        windowStart={windowStart}
        windowEnd={windowEnd}
        minDate={minDate}
        maxDate={maxDate}
        storageKey={`roadmap-${props.roadmap.id}`}
        onRefresh={props.refreshRoadmapItems}
        height={650}
        isLoading={props.isRoadmapItemsLoading || updatingId !== null}
        editable={props.isRoadmapManager}
        allowFullScreen
        allowSaveAsImage
        saveImageFileName={props.roadmap.name}
        // The view selector (Timeline/List) renders inside the toolbar, like WaydGrid.
        toolbarRightSlot={props.viewSelector}
        onItemDateChange={onItemDateChange}
        onItemClick={(item) => item.data?.openDrawer(item.id)}
      />
      <RoadmapColorLegend colors={props.roadmap.colors} />
    </>
  )
}

export default RoadmapTimelineV2

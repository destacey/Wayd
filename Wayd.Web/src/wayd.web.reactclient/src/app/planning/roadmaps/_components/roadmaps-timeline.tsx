'use client'

import { FC, ReactNode, useState } from 'react'
import { Button, Card, Divider, Flex, Slider, Space, Switch, Tooltip, Typography } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import {
  RoadmapActivityListDto,
  RoadmapDetailsDto,
  RoadmapItemListDto,
  RoadmapMilestoneListDto,
  RoadmapTimeboxListDto,
  UpdateRoadmapActivityDatesRequest,
} from '@/src/services/wayd-api'
import { ControlItemsMenu } from '@/src/components/common/control-items-menu'
import {
  WaydDataGroup,
  WaydTimeline,
  WaydTimelineOptions,
} from '@/src/components/common/timeline'
import {
  ItemTemplateProps,
  WaydDataItem,
  TimelineTemplate,
} from '@/src/components/common/timeline/types'
import dayjs from 'dayjs'
import { MinusSquareOutlined, PlusSquareOutlined } from '@ant-design/icons'
import { getLuminance } from '@/src/utils/color-helper'
import { useUpdateRoadmapItemDatesMutation } from '@/src/store/features/planning/roadmaps-api'
import { DateType, TimelineItem } from 'vis-timeline/standalone'
import { useMessage } from '@/src/components/contexts/messaging'
import { isApiError, type ApiError } from '@/src/utils'
import { useAppDispatch, useAppSelector } from '@/src/hooks'
import {
  DEFAULT_GROUP_COLUMN_WIDTH,
  MAX_GROUP_COLUMN_WIDTH,
  MIN_GROUP_COLUMN_WIDTH,
  resetRoadmapGroupColumnWidth,
  setRoadmapGroupColumnWidth,
} from '@/src/store/features/planning/roadmap-timeline-settings-slice'

const { Text } = Typography

export interface RoadmapsTimelineProps {
  roadmap: RoadmapDetailsDto
  roadmapItems: RoadmapItemListDto[]
  isRoadmapItemsLoading: boolean
  refreshRoadmapItems: () => void
  viewSelector?: ReactNode
  openRoadmapItemDrawer: (itemId: string) => void
  isRoadmapManager: boolean
}

interface RoadmapTimelineItem extends WaydDataItem<RoadmapItemListDto, string> {
  id: string
  end: Date
  order?: number
  treeLevel: number
  openRoadmapItemDrawer: (itemId: string) => void
}

enum RoadmapItemType {
  Roadmap = 'roadmap',
  Activity = 'activity',
  Milestone = 'milestone',
  Timebox = 'timebox',
}

interface ProcessedRoadmapData {
  items: RoadmapTimelineItem[]
  maxLevel: number
}

const mapToRequestValues = (
  start: DateType,
  end: DateType,
  type: string,
  itemId: string,
  roadmapId: string,
): UpdateRoadmapActivityDatesRequest => {
  return {
    $type: type,
    roadmapId,
    itemId,
    start: dayjs(start)?.format('YYYY-MM-DD') as unknown as Date, // The type requires it to be a Date, but endpoint requires a string
    end: dayjs(end)?.format('YYYY-MM-DD') as unknown as Date, // The type requires it to be a Date, but endpoint requires a string
  } satisfies UpdateRoadmapActivityDatesRequest
}

function flattenRoadmapItems(
  items: RoadmapItemListDto[],
  openRoadmapItemDrawer: (itemId: string) => void,
  treeLevel: number = 1,
): RoadmapTimelineItem[] {
  return items.reduce<RoadmapTimelineItem[]>((acc, item) => {
    const baseTimelineItem: Partial<RoadmapTimelineItem> = {
      id: String(item.id),
      title: item.name,
      content: item.name,
      itemColor: item.color,
      group: item.parent?.id ? String(item.parent?.id) : undefined,
      treeLevel: treeLevel,
      objectData: item,
      openRoadmapItemDrawer: openRoadmapItemDrawer,
    }

    switch (item.$type) {
      case RoadmapItemType.Activity: {
        const activityItem = item as RoadmapActivityListDto
        const timelineItem: RoadmapTimelineItem = {
          ...baseTimelineItem,
          type: 'range',
          start: dayjs(activityItem.start).toDate(),
          end: dayjs(activityItem.end).toDate(),
          order: activityItem.order,
        } as RoadmapTimelineItem

        acc.push(timelineItem)

        if ((activityItem.children?.length ?? 0) > 0) {
          acc.push(
            ...flattenRoadmapItems(
              activityItem.children ?? [],
              openRoadmapItemDrawer,
              treeLevel + 1,
            ),
          )
        }
        break
      }

      case RoadmapItemType.Milestone: {
        const milestoneItem = item as RoadmapMilestoneListDto
        acc.push({
          ...baseTimelineItem,
          type: 'point',
          start: dayjs(milestoneItem.date).toDate(),
        } as RoadmapTimelineItem)
        break
      }

      case RoadmapItemType.Timebox: {
        const timeboxItem = item as RoadmapTimeboxListDto
        acc.push({
          ...baseTimelineItem,
          type: 'background',
          start: dayjs(timeboxItem.start).toDate(),
          end: dayjs(timeboxItem.end).toDate(),
        } as RoadmapTimelineItem)
        break
      }

      case RoadmapItemType.Roadmap: {
        const activityItem = item as RoadmapActivityListDto
        const timelineItem: RoadmapTimelineItem = {
          ...baseTimelineItem,
          treeLevel: 0,
          type: 'range',
          start: dayjs(activityItem.start).toDate(),
          end: dayjs(activityItem.end).toDate(),
          order: 1,
        } as RoadmapTimelineItem

        acc.push(timelineItem)

        if ((activityItem.children?.length ?? 0) > 0) {
          acc.push(
            ...flattenRoadmapItems(
              activityItem.children ?? [],
              openRoadmapItemDrawer,
              1,
            ),
          )
        }
        break
      }
    }

    return acc
  }, [])
}

function createNestedGroups(
  items: RoadmapTimelineItem[],
  currentLevel: number,
): WaydDataGroup<RoadmapItemListDto>[] {
  // Get all items up to but not including current level
  const groupItems = items.filter((item) => item.treeLevel < currentLevel)

  // Create a map to store parent-child relationships
  const parentChildMap = new Map<string, string[]>()

  // Build the parent-child relationships
  groupItems.forEach((item) => {
    if (item.group) {
      const parentChildren = parentChildMap.get(item.group) || []
      parentChildren.push(item.id)
      parentChildMap.set(item.group, parentChildren)
    }
  })

  // Create the nested groups structure - include all items
  const groups: WaydDataGroup<RoadmapItemListDto>[] = groupItems.map(
    (item) => {
      const isRoadmapRoot = item.objectData?.$type === RoadmapItemType.Roadmap

      return {
        id: item.id,
        // Use NBSP so the label renders with normal height while appearing blank.
        content: isRoadmapRoot ? '\u00A0' : item.objectData?.name || '',
        className: isRoadmapRoot
          ? 'roadmap-group roadmap-root-group'
          : 'roadmap-group',
        nestedGroups: isRoadmapRoot
          ? undefined
          : parentChildMap.get(item.id) || undefined,
        // Shift levels down by one visually because the roadmap root is hidden.
        // This keeps level-1 activities flush-left, while deeper levels still nest.
        treeLevel: Math.max(0, item.treeLevel - 1),
        objectData: item.objectData,
      }
    },
  )

  return groups
}

export const DrillThroughControl = (props: {
  currentLevel: number
  maxLevel: number
  onLevelChange: (treeLevel: number) => void
}) => {
  const onLevelDown = () => {
    props.onLevelChange(props.currentLevel - 1)
  }

  const onLevelUp = () => {
    props.onLevelChange(props.currentLevel + 1)
  }

  return (
    <div>
      <Button
        type="text"
        shape="circle"
        color="default"
        icon={<MinusSquareOutlined />}
        disabled={props.currentLevel === 1}
        onClick={onLevelDown}
      />
      <Button
        type="text"
        shape="circle"
        color="default"
        icon={<PlusSquareOutlined />}
        disabled={props.currentLevel === props.maxLevel || props.maxLevel === 0}
        onClick={onLevelUp}
      />
    </div>
  )
}

export const RoadmapRangeItemTemplate: TimelineTemplate<
  RoadmapTimelineItem
> = ({ item, fontColor, foregroundColor }) => {
  const adjustedfontColor =
    getLuminance(item.itemColor ?? '') > 0.6 ? '#4d4d4d' : '#FFFFFF'

  return (
    <Text style={{ padding: '5px', color: adjustedfontColor }}>
      <a
        onClick={(e) => {
          e.stopPropagation()
          item.openRoadmapItemDrawer(item.id)
        }}
        onMouseDown={(e) => e.stopPropagation()}
        onPointerDown={(e) => e.stopPropagation()}
        style={{ color: adjustedfontColor, textDecoration: 'none' }}
        onMouseOver={(e) =>
          (e.currentTarget.style.textDecoration = 'underline')
        }
        onMouseOut={(e) => (e.currentTarget.style.textDecoration = 'none')}
      >
        {item.content}
      </a>
    </Text>
  )
}

const RoadmapsTimeline = (props: RoadmapsTimelineProps) => {
  // timelineStart / timelineEnd are derived synchronously from props below
  const dispatch = useAppDispatch()
  const [userSelectedLevel, setUserSelectedLevel] = useState<
    number | undefined
  >(undefined)

  const [showCurrentTime, setShowCurrentTime] = useState<boolean>(true)
  const roadmapId = String(props.roadmap?.id ?? '')
  const groupColumnWidth = useAppSelector(
    (state) =>
      state.roadmapTimelineSettings.groupColumnWidthByRoadmapId[roadmapId] ??
      DEFAULT_GROUP_COLUMN_WIDTH,
  )

  const messageApi = useMessage()

  const [updateRoadmapItemDates, { error: updateDatesError }] =
    useUpdateRoadmapItemDatesMutation()

  const processedData: ProcessedRoadmapData | null = (() => {
    if (!props.roadmap || props.isRoadmapItemsLoading || !props.roadmapItems) {
      return null
    }

    // TODO: this is a hack to get the roadmap itself as an item
    const roadmapAsItem: RoadmapActivityListDto = {
      $type: RoadmapItemType.Roadmap,
      id: String(props.roadmap.id),
      roadmapId: props.roadmap.id,
      name: props.roadmap.name,
      type: {
        id: 1,
        name: 'Roadmap',
      },
      start: props.roadmap.start,
      end: props.roadmap.end,
      children: props.roadmapItems.map((item) => ({
        ...item,
        parent: { ...props.roadmap, id: props.roadmap.id },
      })),
    }

    const items = flattenRoadmapItems(
      [roadmapAsItem],
      props.openRoadmapItemDrawer,
    )

    const maxLevel = items.reduce(
      (max, item) => Math.max(max, item.treeLevel),
      0,
    )

    return { items, maxLevel }
  })()

  // Compute auto-drill level synchronously to avoid race condition with groups
  const autoLevel = !processedData ? 1 : processedData.maxLevel > 1 ? 2 : 1

  // User's choice takes precedence over auto-drill, clamped to valid range
  const maxLevel = processedData?.maxLevel ?? 0
  const currentLevel = Math.min(
    userSelectedLevel ?? autoLevel,
    Math.max(maxLevel, 1),
  )

  const visibleItems =
    processedData?.items.filter(
      (item) =>
        item.treeLevel === currentLevel ||
        (item.treeLevel < currentLevel &&
          item.objectData?.$type !== RoadmapItemType.Roadmap &&
          item.objectData?.$type !== RoadmapItemType.Activity),
    ) ?? []

  const processedGroups = (() => {
    if (!processedData || currentLevel <= 1) return undefined

    const potentialGroups = processedData.items.filter(
      (item) =>
        item.objectData?.$type === RoadmapItemType.Activity ||
        item.objectData?.$type === RoadmapItemType.Roadmap,
    )

    return createNestedGroups(potentialGroups, currentLevel)
  })()

  const filteredItems = visibleItems

  // Compute timeline window synchronously from props so the timeline receives values on first render
  const timelineWindow = !props.roadmap
    ? (() => {
        const now = dayjs()
        return { start: now.toDate(), end: now.toDate() }
      })()
    : { start: props.roadmap.start, end: props.roadmap.end }

  const timelineOptions: WaydTimelineOptions<RoadmapTimelineItem> = {
    // TODO: start,end,min,max types don't allow undefined, but initial state is undefined
    showCurrentTime: showCurrentTime,
    maxHeight: 650,
    start: timelineWindow.start,
    end: timelineWindow.end,
    min: timelineWindow.start,
    max: timelineWindow.end,
    groupColumnWidth,
  }

  const onShowCurrentTimeChange = (checked: boolean) => {
    setShowCurrentTime(checked)
  }

  const controlItems: ItemType[] = [
    {
      key: 'group-width-control',
      label: (
        <div
          onClick={(e) => e.stopPropagation()}
          onMouseDown={(e) => e.stopPropagation()}
          style={{ width: 260, padding: '4px 0' }}
        >
          <Space orientation="vertical" size={8} style={{ width: '100%' }}>
            <Tooltip title="Resize the left group-label column">
              <Text type="secondary">Group Width</Text>
            </Tooltip>
            <Space align="center" size={6}>
              <Slider
                min={MIN_GROUP_COLUMN_WIDTH}
                max={MAX_GROUP_COLUMN_WIDTH}
                step={10}
                value={groupColumnWidth}
                onChange={(value) =>
                  dispatch(
                    setRoadmapGroupColumnWidth(
                      {
                        roadmapId,
                        width: Array.isArray(value) ? value[0] : value,
                      },
                    ),
                  )
                }
                tooltip={{ formatter: (value) => `${value}px` }}
                style={{ width: 140, margin: 0 }}
              />
              <Text type="secondary" style={{ minWidth: 48, textAlign: 'right' }}>
                {groupColumnWidth}px
              </Text>
              <Button
                size="small"
                type="text"
                onClick={() =>
                  dispatch(resetRoadmapGroupColumnWidth({ roadmapId }))
                }
              >
                Reset
              </Button>
            </Space>
          </Space>
        </div>
      ),
    },
    {
      label: (
        <Space>
          <Switch
            size="small"
            checked={showCurrentTime}
            onChange={onShowCurrentTimeChange}
          />
          Show Current Time
        </Space>
      ),
      key: 'show-current-time',
      onClick: () => setShowCurrentTime(!showCurrentTime),
    },
  ]

  const onLevelChange = (treeLevel: number) => {
    setUserSelectedLevel(treeLevel)
  }

  const onMove = async (item: TimelineItem) => {
    const originalItem = processedData?.items.find((i) => i.id === item.id)

    if (!originalItem) return

    const { objectData } = originalItem

    if (!objectData) return

    try {
      const value = mapToRequestValues(
        item.start,
        item.end!,
        objectData.$type,
        originalItem.id,
        objectData.roadmapId,
      )

      const response = await updateRoadmapItemDates(value)
      if (response.error) {
        throw response.error
      }
      console.log('Update roadmap activity dates')
    } catch (error) {
      const apiError: ApiError = isApiError(error) ? error : {}
      messageApi.error(
        apiError.detail ??
          'An error occurred while updating the roadmap activity. Please try again.',
      )
      console.error('Error updating roadmap activity dates', error)
    }
  }

  return (
    <>
      <Flex
        justify="space-between"
        align="center"
        style={{ paddingTop: '8px', paddingBottom: '4px' }}
      >
        <DrillThroughControl
          currentLevel={currentLevel}
          maxLevel={processedData?.maxLevel ?? 0}
          onLevelChange={onLevelChange}
        />
        <Flex justify="end" align="center">
          <ControlItemsMenu items={controlItems} />
          <Divider vertical style={{ height: '30px' }} />
          {props.viewSelector}
        </Flex>
      </Flex>
      <Card size="small" variant="borderless">
        <WaydTimeline
          data={filteredItems}
          groups={processedGroups}
          isLoading={props.isRoadmapItemsLoading}
          options={timelineOptions as WaydTimelineOptions<WaydDataItem>}
          rangeItemTemplate={RoadmapRangeItemTemplate as FC<ItemTemplateProps<WaydDataItem>>}
          allowFullScreen={true}
          allowSaveAsImage={true}
          onMove={props.isRoadmapManager ? onMove : undefined}
        />
      </Card>
    </>
  )
}

export default RoadmapsTimeline

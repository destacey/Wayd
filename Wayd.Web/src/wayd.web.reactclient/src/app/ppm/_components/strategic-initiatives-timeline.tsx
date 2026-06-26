'use client'

import { ControlItemsMenu } from '@/src/components/common/control-items-menu'
import {
  ItemTemplateProps,
  WaydDataItem,
  WaydTimeline,
  WaydTimelineOptions,
  TimelineTemplate,
} from '@/src/components/common/timeline'
import { StrategicInitiativeListDto } from '@/src/services/wayd-api'
import { Card, Divider, Flex, Space, Switch, theme, Typography } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import dayjs from 'dayjs'
import { FC, ReactNode, useState } from 'react'
import { StrategicInitiativeDrawer } from '.'
import { getLifecyclePhaseColorFromStatus, getLuminance } from '@/src/utils'

const { Text } = Typography
const { useToken } = theme

export interface StrategicInitiativesTimelineProps {
  strategicInitiatives: StrategicInitiativeListDto[]
  isLoading: boolean
  refetch: () => void
  viewSelector?: ReactNode
  onRefresh?: () => void
}

interface StrategicInitiativeTimelineItem extends WaydDataItem<
  StrategicInitiativeListDto,
  string
> {
  id: string
  openStrategicInitiativeDrawer: (strategicInitiativeKey: number) => void
}

export const StrategicInitiativeRangeItemTemplate: TimelineTemplate<
  StrategicInitiativeTimelineItem
> = ({ item, fontColor, foregroundColor }) => {
  const adjustedfontColor =
    getLuminance(item.itemColor ?? '') > 0.6 ? '#4d4d4d' : '#FFFFFF'

  return (
    <Text style={{ padding: '5px' }}>
      <a
        onClick={() => item.openStrategicInitiativeDrawer(item.objectData!.key)}
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

const StrategicInitiativesTimeline: FC<StrategicInitiativesTimelineProps> = (
  props,
) => {
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [selectedItemKey, setSelectedItemKey] = useState<number | null>(null)
  const [showCurrentTime, setShowCurrentTime] = useState<boolean>(true)
  const { token } = useToken()

  const showDrawer = () => {
    setDrawerOpen(true)
  }

  const onDrawerClose = () => {
    setDrawerOpen(false)
    setSelectedItemKey(null)
  }

  const openStrategicInitiativeDrawer = (strategicInitiativeKey: number) => {
    setSelectedItemKey(strategicInitiativeKey)
    showDrawer()
  }

  const processedStrategicInitiatives: StrategicInitiativeTimelineItem[] =
    props.isLoading || !props.strategicInitiatives
      ? []
      : props.strategicInitiatives
          .filter((initiative) => initiative.start && initiative.end)
          .map((initiative) => ({
            id: String(initiative.id),
            title: initiative.name,
            content: initiative.name,
            itemColor: getLifecyclePhaseColorFromStatus(
              initiative.status,
              token,
            ),
            objectData: initiative,
            type: 'range',
            start: new Date(initiative.start!),
            end: new Date(initiative.end!),
            openStrategicInitiativeDrawer: openStrategicInitiativeDrawer,
          }))

  const timelineWindow = (() => {
    let minDate = dayjs()
    let maxDate = dayjs()

    processedStrategicInitiatives.forEach((initiative) => {
      if (initiative.start && dayjs(initiative.start).isBefore(minDate)) {
        minDate = dayjs(initiative.start)
      }
      if (initiative.end && dayjs(initiative.end).isAfter(maxDate)) {
        maxDate = dayjs(initiative.end)
      }
    })

    minDate = minDate.subtract(14, 'days')
    maxDate = maxDate.add(1, 'month')

    return { start: minDate.toDate(), end: maxDate.toDate() }
  })()

  const timelineOptions: WaydTimelineOptions<StrategicInitiativeTimelineItem> =
    {
      showCurrentTime: showCurrentTime,
      maxHeight: 650,
      start: timelineWindow.start,
      end: timelineWindow.end,
      min: timelineWindow.start,
      max: timelineWindow.end,
    }

  const onShowCurrentTimeChange = (checked: boolean) => {
    setShowCurrentTime(checked)
  }

  const controlItems = (): ItemType[] => {
    const items: ItemType[] = []

    items.push({
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
    })

    return items
  }

  return (
    <>
      <Flex justify="end" align="center">
        <ControlItemsMenu items={controlItems()} />
        <Divider vertical style={{ height: '30px' }} />
        {props.viewSelector}
      </Flex>
      <Card size="small" variant="borderless">
        <WaydTimeline
          data={processedStrategicInitiatives}
          isLoading={props.isLoading}
          options={timelineOptions as WaydTimelineOptions<WaydDataItem>}
          rangeItemTemplate={StrategicInitiativeRangeItemTemplate as FC<ItemTemplateProps<WaydDataItem>>}
          allowFullScreen={true}
          allowSaveAsImage={true}
        />
      </Card>
      {selectedItemKey && (
        <StrategicInitiativeDrawer
          strategicInitiativeKey={selectedItemKey}
          drawerOpen={drawerOpen}
          onDrawerClose={onDrawerClose}
        />
      )}
    </>
  )
}

export default StrategicInitiativesTimeline

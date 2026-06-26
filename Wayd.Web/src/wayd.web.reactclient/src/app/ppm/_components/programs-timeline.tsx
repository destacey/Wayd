'use client'

import { ControlItemsMenu } from '@/src/components/common/control-items-menu'
import {
  ItemTemplateProps,
  WaydDataItem,
  WaydTimeline,
  WaydTimelineOptions,
  TimelineTemplate,
} from '@/src/components/common/timeline'
import { ProgramListDto } from '@/src/services/wayd-api'
import { Card, Divider, Flex, Space, Switch, theme, Typography } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import dayjs from 'dayjs'
import { FC, ReactNode, useState } from 'react'
import { ProgramDrawer } from '.'
import { getLifecyclePhaseColorFromStatus, getLuminance } from '@/src/utils'

const { Text } = Typography
const { useToken } = theme

export interface ProgramsTimelineProps {
  programs: ProgramListDto[]
  isLoading: boolean
  refetch: () => void
  viewSelector?: ReactNode
  onRefresh?: () => void
}

interface ProgramTimelineItem extends WaydDataItem<ProgramListDto, string> {
  id: string
  openProgramDrawer: (programKey: number) => void
}

export const ProgramRangeItemTemplate: TimelineTemplate<
  ProgramTimelineItem
> = ({ item, fontColor, foregroundColor }) => {
  const adjustedfontColor =
    getLuminance(item.itemColor ?? '') > 0.6 ? '#4d4d4d' : '#FFFFFF'

  return (
    <Text style={{ padding: '5px' }}>
      <a
        onClick={() => item.openProgramDrawer(item.objectData!.key)}
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

const ProgramsTimeline: React.FC<ProgramsTimelineProps> = (props) => {
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

  const openProgramDrawer = (programKey: number) => {
    setSelectedItemKey(programKey)
    showDrawer()
  }

  // Derive timeline items and window synchronously so the timeline receives data on first render
  const processedPrograms: ProgramTimelineItem[] =
    props.isLoading || !props.programs
      ? []
      : props.programs
          .filter((program) => program.start && program.end)
          .map((program) => ({
            id: String(program.id),
            title: program.name,
            content: program.name,
            itemColor: getLifecyclePhaseColorFromStatus(program.status, token),
            objectData: program,
            type: 'range',
            start: new Date(program.start!),
            end: new Date(program.end!),
            openProgramDrawer: openProgramDrawer,
          }))

  const timelineWindow = (() => {
    let minDate = dayjs()
    let maxDate = dayjs()

    processedPrograms.forEach((program) => {
      if (program.start && dayjs(program.start).isBefore(minDate)) {
        minDate = dayjs(program.start)
      }
      if (program.end && dayjs(program.end).isAfter(maxDate)) {
        maxDate = dayjs(program.end)
      }
    })

    minDate = minDate.subtract(14, 'days')
    maxDate = maxDate.add(1, 'month')

    return { start: minDate.toDate(), end: maxDate.toDate() }
  })()

  const timelineOptions: WaydTimelineOptions<ProgramTimelineItem> = {
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

  const isLoading = props.isLoading

  return (
    <>
      <Flex justify="end" align="center">
        <ControlItemsMenu items={controlItems()} />
        <Divider vertical style={{ height: '30px' }} />
        {props.viewSelector}
      </Flex>
      <Card size="small" variant="borderless">
        <WaydTimeline
          data={processedPrograms}
          isLoading={isLoading}
          options={timelineOptions as WaydTimelineOptions<WaydDataItem>}
          rangeItemTemplate={ProgramRangeItemTemplate as FC<ItemTemplateProps<WaydDataItem>>}
          allowFullScreen={true}
          allowSaveAsImage={true}
        />
      </Card>
      {selectedItemKey && (
        <ProgramDrawer
          programKey={selectedItemKey}
          drawerOpen={drawerOpen}
          onDrawerClose={onDrawerClose}
        />
      )}
    </>
  )
}

export default ProgramsTimeline

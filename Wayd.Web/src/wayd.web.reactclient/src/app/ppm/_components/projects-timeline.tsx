'use client'

import { ControlItemsMenu } from '@/src/components/common/control-items-menu'
import {
  ItemTemplateProps,
  WaydDataItem,
  WaydTimeline,
  WaydTimelineOptions,
  TimelineTemplate,
} from '@/src/components/common/timeline'
import { ProjectListDto } from '@/src/services/wayd-api'
import { Card, Divider, Flex, Space, Switch, theme, Typography } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import dayjs from 'dayjs'
import { FC, ReactNode, useState } from 'react'
import { ProjectDrawer } from '.'
import { DataGroup } from 'vis-timeline/standalone'
import { getLifecyclePhaseColorFromStatus, getLuminance } from '@/src/utils'

const { Text } = Typography
const { useToken } = theme

export interface ProjectsTimelineProps {
  projects: ProjectListDto[]
  isLoading: boolean
  refetch: () => void
  viewSelector?: ReactNode
  groupByProgram?: boolean
  onRefresh?: () => void
}

interface ProjectTimelineItem extends WaydDataItem<ProjectListDto, string> {
  id: string
  openProjectDrawer: (projectKey: string) => void
}

export const ProjectRangeItemTemplate: TimelineTemplate<
  ProjectTimelineItem
> = ({ item, fontColor, foregroundColor }) => {
  const adjustedfontColor =
    getLuminance(item.itemColor ?? '') > 0.6 ? '#4d4d4d' : '#FFFFFF'

  return (
    <Text style={{ padding: '5px' }}>
      <a
        onClick={() => item.openProjectDrawer(item.objectData!.key)}
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

const ProjectsTimeline: FC<ProjectsTimelineProps> = (props) => {
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [selectedItemKey, setSelectedItemKey] = useState<string | null>(null)
  const [showCurrentTime, setShowCurrentTime] = useState<boolean>(true)
  const { token } = useToken()

  const showDrawer = () => {
    setDrawerOpen(true)
  }

  const onDrawerClose = () => {
    setDrawerOpen(false)
    setSelectedItemKey(null)
  }

  const openProjectDrawer = (projectKey: string) => {
    setSelectedItemKey(projectKey)
    showDrawer()
  }

  const groups: DataGroup[] = (() => {
    if (!props.groupByProgram) {
      return []
    }

    const programGroups: { [key: string]: DataGroup } = {}

    props.projects.forEach((project) => {
      const programName = project.program?.name || 'No Program'
      if (!programGroups[programName]) {
        programGroups[programName] = {
          id: programName,
          content: programName,
        }
      }
    })

    const groups = Object.values(programGroups)

    // if the only group is "No Program", return an empty list
    if (groups.length === 1 && groups[0].id === 'No Program') {
      return []
    }

    // return alphabetically sorted groups with 'No Program' last
    return groups.sort((a, b) => {
      if (a.id === 'No Program') return 1
      if (b.id === 'No Program') return -1
      return String(a.id).localeCompare(String(b.id))
    })
  })()

  // Derive timeline items and window synchronously so the timeline receives data on first render
  const processedProjects: ProjectTimelineItem[] =
    props.isLoading || !props.projects
      ? []
      : props.projects
          .filter((project) => project.start && project.end)
          .map((project) => ({
            id: String(project.id),
            title: project.name,
            content: project.name,
            itemColor: getLifecyclePhaseColorFromStatus(project.status, token),
            objectData: project,
            group: project.program?.name ?? 'No Program',
            type: 'range',
            start: new Date(project.start!),
            end: new Date(project.end!),
            openProjectDrawer: openProjectDrawer,
          }))

  const timelineWindow = (() => {
    let minDate = dayjs()
    let maxDate = dayjs()

    processedProjects.forEach((project) => {
      if (project.start && dayjs(project.start).isBefore(minDate)) {
        minDate = dayjs(project.start)
      }
      if (project.end && dayjs(project.end).isAfter(maxDate)) {
        maxDate = dayjs(project.end)
      }
    })

    minDate = minDate.subtract(14, 'days')
    maxDate = maxDate.add(1, 'month')

    return { start: minDate.toDate(), end: maxDate.toDate() }
  })()

  const projectsNoDatesCount = props.projects.filter(
    (project) => !project.start,
  ).length

  const timelineOptions: WaydTimelineOptions<ProjectTimelineItem> = {
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
          data={processedProjects}
          groups={groups.length > 0 ? groups : undefined}
          isLoading={props.isLoading}
          options={timelineOptions as WaydTimelineOptions<WaydDataItem>}
          rangeItemTemplate={ProjectRangeItemTemplate as FC<ItemTemplateProps<WaydDataItem>>}
          allowFullScreen={true}
          allowSaveAsImage={true}
        />
      </Card>
      {projectsNoDatesCount > 0 && (
        <Text type="warning">
          Note: {projectsNoDatesCount}{' '}
          {projectsNoDatesCount === 1 ? 'project is' : 'projects are'} not shown
          on the timeline due to missing dates.
        </Text>
      )}
      {selectedItemKey && (
        <ProjectDrawer
          projectKey={selectedItemKey}
          drawerOpen={drawerOpen}
          onDrawerClose={onDrawerClose}
        />
      )}
    </>
  )
}

export default ProjectsTimeline

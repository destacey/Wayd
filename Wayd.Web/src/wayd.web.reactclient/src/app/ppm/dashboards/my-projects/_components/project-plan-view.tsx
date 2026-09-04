'use client'

import { getAvatarColor } from '@/src/utils'
import {
  ProjectPlanNodeDto,
  EmployeeNavigationDto,
} from '@/src/services/wayd-api'
import { useGetProjectPlanTreeQuery } from '@/src/store/features/ppm/projects-api'
import {
  CheckCircleFilled,
  ClockCircleOutlined,
  MinusCircleOutlined,
  RightOutlined,
  SyncOutlined,
} from '@ant-design/icons'
import { Avatar, Flex, Popover, Progress, Skeleton, Tag, Typography } from 'antd'
import { WaydTooltip } from '@/src/components/common'
import dayjs from 'dayjs'
import { FC, useState } from 'react'
import { getInitials } from './project-card-helpers'
import {
  getPlanScheduleLabel,
  type PlanScheduleLabel,
} from '@/src/app/ppm/projects/_components/project-plan-schedule'
import styles from '../my-projects-dashboard.module.css'

const { Text } = Typography

// --- Stage tag color ---

function getStageTagColor(statusName: string): string {
  switch (statusName) {
    case 'Completed':
      return 'success'
    case 'In Progress':
      return 'processing'
    case 'Canceled':
      return 'error'
    default:
      return 'default'
  }
}

// --- Task status helpers ---

type TaskStatusLabel = PlanScheduleLabel | 'Complete'

/**
 * The badge shown on a task row.
 *
 * The schedule buckets come from the shared helper, so these badges agree with
 * the plan grid's Schedule column and with the summary counts — all three read
 * the same Saturday week boundary, and a task due today is Due This Week rather
 * than a bucket of its own. "Complete" is the dashboard's own addition: the
 * grid leaves finished tasks blank, but a completed row here earns a badge.
 */
function getTaskStatusLabel(node: ProjectPlanNodeDto): TaskStatusLabel {
  if (node.status?.name === 'Completed') return 'Complete'

  return getPlanScheduleLabel(node)
}

function getTaskStatusClass(label: TaskStatusLabel): string {
  switch (label) {
    case 'Overdue':
      return styles.taskStatusOverdue
    case 'Due This Week':
      return styles.taskStatusDueThisWeek
    case 'Upcoming':
      return styles.taskStatusUpcoming
    case 'Complete':
      return styles.taskStatusComplete
    default:
      return ''
  }
}

// --- Priority indicator ---

function getPriorityColor(priority?: string): string | undefined {
  switch (priority) {
    case 'Critical':
      return 'var(--ant-color-error)'
    case 'High':
      return 'var(--ant-color-warning)'
    case 'Medium':
      return 'var(--ant-color-primary)'
    case 'Low':
      return 'var(--ant-color-text-quaternary)'
    default:
      return undefined
  }
}

// --- Assignee avatars (inline, small) ---

const TaskAssignees: FC<{ assignees: EmployeeNavigationDto[] }> = ({
  assignees,
}) => {
  if (assignees.length === 0) return null
  return (
    <Avatar.Group size={20}>
      {assignees.slice(0, 3).map((a) => (
        <WaydTooltip key={a.id} title={a.name}>
          <Avatar
            size={20}
            style={{
              backgroundColor: getAvatarColor(a.id),
              fontSize: 9,
              fontWeight: 600,
            }}
          >
            {getInitials(a.name)}
          </Avatar>
        </WaydTooltip>
      ))}
    </Avatar.Group>
  )
}

// --- Task Row ---

function getTaskIcon(statusName: string | undefined) {
  switch (statusName) {
    case 'Completed':
      return <CheckCircleFilled style={{ color: 'var(--ant-color-success)', fontSize: 14 }} />
    case 'In Progress':
      return <SyncOutlined style={{ color: 'var(--ant-color-primary)', fontSize: 14 }} />
    case 'Canceled':
      return <MinusCircleOutlined style={{ color: 'var(--ant-color-text-quaternary)', fontSize: 14 }} />
    default:
      return <ClockCircleOutlined style={{ color: 'var(--ant-color-text-quaternary)', fontSize: 14 }} />
  }
}

function buildTaskTooltip(task: ProjectPlanNodeDto) {
  const startDate = task.start ? dayjs(task.start).format('MMM D, YYYY') : null
  const endDate = task.end ? dayjs(task.end).format('MMM D, YYYY') : null
  const plannedDate = task.plannedDate
    ? dayjs(task.plannedDate).format('MMM D, YYYY')
    : null
  const assigneeNames = task.assignees?.map((a) => a.name).join(', ')

  return (
    <div>
      {task.status?.name && <div>Status: {task.status.name}</div>}
      {task.priority?.name && <div>Priority: {task.priority.name}</div>}
      {startDate && endDate && <div>Dates: {startDate} - {endDate}</div>}
      {plannedDate && <div>Planned: {plannedDate}</div>}
      {assigneeNames && <div>Assignees: {assigneeNames}</div>}
      {task.progress > 0 && <div>Progress: {task.progress}%</div>}
    </div>
  )
}

const TaskRow: FC<{ task: ProjectPlanNodeDto }> = ({ task }) => {
  const isCompleted = task.status?.name === 'Completed'
  const statusLabel = getTaskStatusLabel(task)
  const dueDate = task.end ?? task.plannedDate
  const priorityColor = getPriorityColor(task.priority?.name)

  return (
    <Popover content={buildTaskTooltip(task)} trigger="click" placement="top">
      <div
        className={`${styles.taskRow} ${isCompleted ? styles.taskRowCompleted : ''}`}
      >
        {getTaskIcon(task.status?.name)}
        {priorityColor && (
          <span
            style={{
              width: 6,
              height: 6,
              borderRadius: '50%',
              backgroundColor: priorityColor,
              flexShrink: 0,
            }}
          />
        )}
        <Text
          ellipsis
          className={`${styles.taskTitle} ${isCompleted ? styles.taskTitleCompleted : ''}`}
        >
          {task.name}
        </Text>
        <TaskAssignees assignees={task.assignees} />
        {dueDate && (
          <span className={styles.taskDueDate}>
            {dayjs(dueDate).format('MMM D')}
          </span>
        )}
        {statusLabel && (
          <span
            className={`${styles.taskStatusBadge} ${getTaskStatusClass(statusLabel)}`}
          >
            {statusLabel}
          </span>
        )}
      </div>
    </Popover>
  )
}

// --- Deliverable Section ---

interface DeliverableSectionProps {
  node: ProjectPlanNodeDto
  defaultExpanded: boolean
}

const DeliverableSection: FC<DeliverableSectionProps> = ({
  node,
  defaultExpanded,
}) => {
  const [collapsed, setCollapsed] = useState(!defaultExpanded)
  const tasks = node.children ?? []
  const completedCount = tasks.filter(
    (t) => t.status?.name === 'Completed',
  ).length

  return (
    <div className={styles.deliverableSection}>
      <div
        className={styles.deliverableHeader}
        onClick={() => tasks.length > 0 && setCollapsed((c) => !c)}
      >
        {tasks.length > 0 && (
          <RightOutlined
            className={`${styles.collapseIcon} ${!collapsed ? styles.collapseIconExpanded : ''}`}
          />
        )}
        <span className={styles.deliverableName}>{node.name}</span>
        {node.progress != null && node.progress > 0 && (
          <Progress
            percent={node.progress}
            size="small"
            style={{ width: 60 }}
            showInfo={false}
          />
        )}
        <Text type="secondary" style={{ fontSize: 11, whiteSpace: 'nowrap' }}>
          {completedCount}/{tasks.length}
        </Text>
      </div>
      {!collapsed && tasks.length > 0 && (
        <div className={styles.deliverableContent}>
          {tasks.map((task) => (
            <TaskRow key={task.id} task={task} />
          ))}
        </div>
      )}
    </div>
  )
}

// --- Stage Section ---

interface StageSectionProps {
  stage: ProjectPlanNodeDto
  isActive: boolean
}

const StageSection: FC<StageSectionProps> = ({ stage, isActive }) => {
  const [collapsed, setCollapsed] = useState(!isActive)
  const children = stage.children ?? []
  const hasChildren = children.length > 0

  const taskCounts = countTasksByStatus(children)

  return (
    <div className={styles.stageSection}>
      <Flex
        className={styles.stageHeader}
        align="center"
        gap={8}
        wrap
        onClick={() => hasChildren && setCollapsed((c) => !c)}
      >
        {hasChildren && (
          <RightOutlined
            className={`${styles.collapseIcon} ${!collapsed ? styles.collapseIconExpanded : ''}`}
          />
        )}
        <span className={styles.stageName}>{stage.name}</span>
        {stage.status?.name && (
          <Tag color={getStageTagColor(stage.status.name)} style={{ margin: 0 }}>
            {stage.status.name}
          </Tag>
        )}
        <Flex align="center" gap={8} style={{ marginLeft: 'auto' }}>
          {taskCounts.overdue > 0 && (
            <WaydTooltip title={`${taskCounts.overdue} overdue ${taskCounts.overdue === 1 ? 'task' : 'tasks'}`}>
              <span className={`${styles.statPill} ${styles.statPillOverdue}`}>
                {taskCounts.overdue} overdue
              </span>
            </WaydTooltip>
          )}
          {taskCounts.dueThisWeek > 0 && (
            <WaydTooltip title={`${taskCounts.dueThisWeek} ${taskCounts.dueThisWeek === 1 ? 'task' : 'tasks'} due this week`}>
              <span className={`${styles.statPill} ${styles.statPillDueThisWeek}`}>
                {taskCounts.dueThisWeek} this week
              </span>
            </WaydTooltip>
          )}
          {taskCounts.upcoming > 0 && (
            <WaydTooltip title={`${taskCounts.upcoming} upcoming ${taskCounts.upcoming === 1 ? 'task' : 'tasks'}`}>
              <span className={`${styles.statPill} ${styles.statPillUpcoming}`}>
                {taskCounts.upcoming} upcoming
              </span>
            </WaydTooltip>
          )}
          <Progress
            percent={stage.progress}
            size="small"
            style={{ width: 80 }}
          />
        </Flex>
      </Flex>
      {!collapsed && hasChildren && (
        <div className={styles.stageContent}>
          {children.map((child) => {
            if (child.children && child.children.length > 0) {
              return (
                <DeliverableSection
                  key={child.id}
                  node={child}
                  defaultExpanded={isActive}
                />
              )
            }
            return <TaskRow key={child.id} task={child} />
          })}
        </div>
      )}
    </div>
  )
}

interface StageTaskCounts {
  overdue: number
  dueThisWeek: number
  upcoming: number
}

function countTasksByStatus(nodes: ProjectPlanNodeDto[]): StageTaskCounts {
  const counts: StageTaskCounts = { overdue: 0, dueThisWeek: 0, upcoming: 0 }
  for (const node of nodes) {
    const label = getTaskStatusLabel(node)
    if (label === 'Overdue') counts.overdue++
    else if (label === 'Due This Week') counts.dueThisWeek++
    else if (label === 'Upcoming') counts.upcoming++
    if (node.children) {
      const childCounts = countTasksByStatus(node.children)
      counts.overdue += childCounts.overdue
      counts.dueThisWeek += childCounts.dueThisWeek
      counts.upcoming += childCounts.upcoming
    }
  }
  return counts
}

// --- Main Component ---

export interface ProjectPlanViewProps {
  projectKey: string
}

const ProjectPlanView: FC<ProjectPlanViewProps> = ({ projectKey }) => {
  const { data: planTree, isLoading } = useGetProjectPlanTreeQuery(projectKey)

  if (isLoading) return <Skeleton active paragraph={{ rows: 6 }} />
  if (!planTree || planTree.length === 0) {
    return (
      <Text type="secondary" style={{ fontSize: 12 }}>
        No project plan defined.
      </Text>
    )
  }

  const stages = planTree.filter((n) => n.nodeType === 'Stage')

  return (
    <Flex vertical gap={8}>
      {stages.map((stage) => (
        <StageSection
          key={stage.id}
          stage={stage}
          isActive={stage.status?.name === 'In Progress'}
        />
      ))}
    </Flex>
  )
}

export default ProjectPlanView

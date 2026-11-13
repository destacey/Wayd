'use client'

import { WaydList } from '@/src/components/common'
import EntityLink from '@/src/components/common/entity-link'
import { MyProjectTaskDto, ProjectStatus } from '@/src/services/wayd-api'
import { useGetMyProjectTasksQuery } from '@/src/store/features/ppm/projects-api'
import {
  addCalendarDays,
  CalendarDate,
  compareCalendarDates,
  formatCalendarDate,
  parseCalendarDate,
  todayCalendarDate,
} from '@/src/utils'
import { Typography } from 'antd'
import Link from 'next/link'
import { FC } from 'react'
import AttentionTile from '../attention-tile'
import { useHomeContext } from '../use-home-context'
import WidgetCard, { WidgetEmpty, WidgetError } from '../widget-card'
import {
  isOn,
  rowsToShow,
  WidgetSetting,
  WidgetSettingValues,
} from '../widget-settings'
import { HomeWidgetProps, widgetAnchorId } from '../widget-registry'
import styles from '../home.module.css'

const { Text } = Typography

/** The projects whose tasks are live work: the same statuses the home context counts. */
const STATUS_FILTER = { status: [ProjectStatus.Approved, ProjectStatus.Active] }

/** The most tasks the widget lists; the rest are counted below them. */
export const MY_PROJECT_TASKS_LIMIT = 5

/** Where a task's due date falls, from today. */
export type TaskDue = 'overdue' | 'this-week' | 'later' | 'none'

/** The Saturday that ends the week `today` is in (today, when it is Saturday). */
export const endOfWeek = (today: CalendarDate): CalendarDate =>
  addCalendarDays(today, (6 - parseCalendarDate(today).day() + 7) % 7)

/**
 * Where a task's due date falls: past, by the end of this week (Saturday, as
 * the project task metrics count it), later, or not planned.
 */
export const taskDue = (
  task: MyProjectTaskDto,
  today: CalendarDate,
): TaskDue => {
  if (!task.dueDate) return 'none'
  if (compareCalendarDates(task.dueDate, today) < 0) return 'overdue'
  if (compareCalendarDates(task.dueDate, endOfWeek(today)) <= 0)
    return 'this-week'
  return 'later'
}

/**
 * How far ahead the widget lists tasks, in the project plan's own groupings:
 * overdue only, also those due by the end of this week, or also the upcoming
 * ones due by the end of next week. Overdue tasks are always listed.
 */
export type TaskDueWindow = 'overdue' | 'this-week' | 'upcoming'

/** The setting for how far ahead the widget lists tasks. */
export const DUE_WINDOW_SETTING = 'due-window'

/** The setting that decides whether milestones are listed beside tasks. */
export const INCLUDE_MILESTONES_SETTING = 'include-milestones'

const DUE_WINDOWS: TaskDueWindow[] = ['overdue', 'this-week', 'upcoming']

/** What the user can change about the widget, beyond its rows and empty display. */
export const myProjectTasksSettings: WidgetSetting[] = [
  {
    kind: 'choice',
    key: DUE_WINDOW_SETTING,
    label: 'List tasks up to',
    options: [
      { value: 'overdue', label: 'Overdue' },
      { value: 'this-week', label: 'Due this week' },
      { value: 'upcoming', label: 'Upcoming' },
    ],
    default: 'upcoming',
  },
  {
    kind: 'toggle',
    key: INCLUDE_MILESTONES_SETTING,
    label: 'Include milestones',
    default: true,
  },
]

/** The due window in effect: the setting's value, or Upcoming for a widget rendered without settings. */
export const taskDueWindowOf = (
  settings: WidgetSettingValues | undefined,
): TaskDueWindow =>
  DUE_WINDOWS.find((w) => w === settings?.[DUE_WINDOW_SETTING]) ?? 'upcoming'

/** Whether a task is a milestone rather than a task, as the plan names its type. */
export const isMilestone = (task: MyProjectTaskDto): boolean =>
  task.type.name === 'Milestone'

/**
 * The tasks the widget lists — overdue, or due within the window — and how
 * many open tasks fall later or have no due date. The home page is a
 * briefing, so work that is not due soon is counted, not listed.
 */
export const tasksDueSoon = (
  tasks: MyProjectTaskDto[],
  today: CalendarDate,
  dueWindow: TaskDueWindow = 'upcoming',
): { soon: MyProjectTaskDto[]; laterCount: number } => {
  const isListed = (dueDate: CalendarDate) => {
    switch (dueWindow) {
      case 'overdue':
        return compareCalendarDates(dueDate, today) < 0
      case 'this-week':
        return compareCalendarDates(dueDate, endOfWeek(today)) <= 0
      default:
        return (
          compareCalendarDates(dueDate, addCalendarDays(endOfWeek(today), 7)) <=
          0
        )
    }
  }
  const soon = tasks.filter((t) => !!t.dueDate && isListed(t.dueDate))
  return { soon, laterCount: tasks.length - soon.length }
}

/** What the listed tasks are, for the line counting those past the rows shown. */
const listedWording: Record<TaskDueWindow, string> = {
  overdue: 'overdue',
  'this-week': 'due by the end of this week',
  upcoming: 'due by the end of next week',
}

const plural = (n: number, one: string, many: string) =>
  `${n} ${n === 1 ? one : many}`

/** How many are due later, or with no date, said once beneath the list. */
const laterLine = (count: number) =>
  `You have ${plural(count, 'more open task', 'more open tasks')} due later.`

/** How many of the tasks are overdue and how many are due by the end of this week. */
export const countTasksDue = (
  tasks: MyProjectTaskDto[],
  today: CalendarDate,
): { overdue: number; dueThisWeek: number } => {
  const due = tasks.map((t) => taskDue(t, today))
  return {
    overdue: due.filter((d) => d === 'overdue').length,
    dueThisWeek: due.filter((d) => d === 'this-week').length,
  }
}

/**
 * The query the widget and both tiles read; RTK Query shares the request.
 * Skipped when the home context says the caller has no task assigned on an
 * approved or active project.
 */
const useMyTasks = () => {
  const { context } = useHomeContext()
  const hasNoProjects = context?.projects.assigneeCount === 0
  const result = useGetMyProjectTasksQuery(STATUS_FILTER, {
    skip: hasNoProjects,
  })
  return { ...result, hasNoProjects }
}

const Due: FC<{ task: MyProjectTaskDto; today: CalendarDate }> = ({
  task,
  today,
}) => {
  if (!task.dueDate) return <Text type="secondary">No due date</Text>
  const label = `Due ${formatCalendarDate(task.dueDate)}`
  switch (taskDue(task, today)) {
    case 'overdue':
      return <Text type="danger">{label} (overdue)</Text>
    case 'this-week':
      return <Text type="warning">{label}</Text>
    default:
      return <Text type="secondary">{label}</Text>
  }
}

/**
 * The open project tasks and, unless the user leaves them out, milestones
 * assigned to the caller on approved and active projects that are overdue or
 * due within the chosen window, the most urgent first; the rest are counted,
 * not listed.
 */
const MyProjectTasksWidget: FC<HomeWidgetProps> = ({ title, settings }) => {
  const rows = rowsToShow(settings, MY_PROJECT_TASKS_LIMIT)
  const dueWindow = taskDueWindowOf(settings)
  const includeMilestones = isOn(settings, INCLUDE_MILESTONES_SETTING, true)
  const { data: allTasks, isLoading, isError } = useMyTasks()
  const tasks = includeMilestones
    ? allTasks
    : allTasks?.filter((t) => !isMilestone(t))
  // Said apart from having none, since the Overdue and Due this week counts still include the milestones.
  const onlyMilestones =
    !!allTasks && allTasks.length > 0 && !!tasks && tasks.length === 0
  const today = todayCalendarDate()

  const body = () => {
    if (isError) return <WidgetError message="Could not load your tasks." />
    if (onlyMilestones) {
      return (
        <WidgetEmpty message="Every open item assigned to you is a milestone, and this widget is set to leave milestones out." />
      )
    }
    if (!tasks || tasks.length === 0) {
      return (
        <WidgetEmpty message="You have no open project tasks on approved or active projects. They appear here when a project manager assigns you one." />
      )
    }
    const { soon, laterCount } = tasksDueSoon(tasks, today, dueWindow)
    if (soon.length === 0) {
      const nothing =
        dueWindow === 'overdue'
          ? 'Nothing is overdue.'
          : `Nothing is ${listedWording[dueWindow]}.`
      return (
        <WidgetEmpty
          message={`${nothing} You have ${plural(laterCount, 'open task', 'open tasks')} due later.`}
        />
      )
    }
    const more = soon.length - rows
    return (
      <>
        <WaydList size="small">
          {soon.slice(0, rows).map((task) => (
            <WaydList.Item key={task.id} className={styles.listItem}>
              <div className={styles.listItemBody}>
                <EntityLink
                  href={`/ppm/projects/${task.project.key}?section=plan`}
                >
                  {task.key} · {task.name}
                </EntityLink>
                <span className={styles.listItemMeta}>
                  <Due task={task} today={today} />
                  <Link href={`/ppm/projects/${task.project.key}`}>
                    {task.project.name}
                  </Link>
                </span>
              </div>
            </WaydList.Item>
          ))}
        </WaydList>
        {more > 0 ? (
          <Text type="secondary" className={styles.widgetEmpty}>
            And {more} more {listedWording[dueWindow]}.
          </Text>
        ) : null}
        {laterCount > 0 ? (
          <Text type="secondary" className={styles.widgetEmpty}>
            {laterLine(laterCount)}
          </Text>
        ) : null}
      </>
    )
  }

  return (
    <WidgetCard
      title={title}
      loading={isLoading}
      failed={isError}
      // Not when only later tasks remain: they are counted, and hiding them would hide work.
      empty={!tasks || tasks.length === 0}
      emptySummary={
        onlyMilestones ? 'No open tasks (milestones hidden)' : 'No open tasks'
      }
    >
      {body()}
    </WidgetCard>
  )
}

/** The tiles' count: zero when there is known to be nothing, none when it failed. */
const useTaskCounts = () => {
  const { data, isLoading, isError, hasNoProjects } = useMyTasks()
  // A failed refetch keeps the last good data; showing it would pass off an old count as current.
  const counts =
    isError || (!data && !hasNoProjects)
      ? undefined
      : countTasksDue(data ?? [], todayCalendarDate())
  return { counts, isLoading }
}

/** Open tasks assigned to the caller that are past their due date. */
export const OverdueTasksTile: FC = () => {
  const { counts, isLoading } = useTaskCounts()
  return (
    <AttentionTile
      title="Overdue tasks"
      context="assigned to you"
      value={counts?.overdue}
      tooltip="Open tasks and milestones assigned to you on approved and active projects that are past their due date."
      href={`#${widgetAnchorId('my-project-tasks')}`}
      alert="error"
      loading={isLoading}
    />
  )
}

/** Open tasks assigned to the caller that are due by the end of this week. */
export const DueThisWeekTile: FC = () => {
  const { counts, isLoading } = useTaskCounts()
  return (
    <AttentionTile
      title="Due this week"
      context="assigned to you"
      value={counts?.dueThisWeek}
      tooltip="Open tasks and milestones assigned to you on approved and active projects that are due by the end of this week (Saturday)."
      href={`#${widgetAnchorId('my-project-tasks')}`}
      alert="warning"
      loading={isLoading}
    />
  )
}

export default MyProjectTasksWidget

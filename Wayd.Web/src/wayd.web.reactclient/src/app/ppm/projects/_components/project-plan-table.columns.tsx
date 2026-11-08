'use client'

import { ProjectPlanNodeDto } from '@/src/services/wayd-api'
import {
  CaretDownOutlined,
  CaretRightOutlined,
  HolderOutlined,
  MoreOutlined,
  PlusOutlined,
} from '@ant-design/icons'
import { Button, Dropdown, Flex, Tag } from 'antd'
import { formatCalendarDate } from '@/src/utils/calendar-date'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import styles from '@/src/components/common/wayd-grid/wayd-grid.module.css'
import {
  type CellEditor,
  type FilterOption,
  type WaydGridColumnMeta,
  TagListCell,
  dateSortBy,
  useGridDragHandle,
} from '@/src/components/common/wayd-grid'
import {
  getPlanScheduleLabel,
  isPlanItemOverdue,
} from './project-plan-schedule'
import {
  isMilestoneRow,
  isStageRow,
  planEffortEditor,
  planEndEditor,
  planNameEditor,
  planProgressEditor,
  planStartEditor,
} from './project-plan-table.editors'

const overdueDateStyle: React.CSSProperties = {
  color: 'var(--ant-color-error)',
}

const scheduleTagColors: Record<string, string> = {
  Overdue: 'error',
  'Due This Week': 'warning',
  Upcoming: 'default',
}

const statusTagColors: Record<string, string> = {
  'Not Started': 'default',
  'In Progress': 'processing',
  Completed: 'success',
  Canceled: 'error',
}

const DATE_FORMAT = 'MMM D, YYYY'
const formatDate = (value: string | undefined | null) =>
  formatCalendarDate(value, DATE_FORMAT)

const NAME_TEXT_STYLE = {
  flex: 1,
  minWidth: 0,
  overflow: 'hidden',
  textOverflow: 'ellipsis',
} as const

// Drag handle component that uses the context from the sortable row
function DragHandleCell({ isDragEnabled }: { isDragEnabled: boolean }) {
  const { listeners, attributes } = useGridDragHandle()

  if (!isDragEnabled) {
    return null
  }

  return (
    <div
      {...listeners}
      {...attributes}
      className={styles.dragHandle}
      onClick={(e) => e.stopPropagation()}
      title="Drag to reorder or change parent"
      style={{
        cursor: 'grab',
        touchAction: 'none',
      }}
    >
      <HolderOutlined
        style={{ fontSize: 14, color: 'var(--ant-color-text-tertiary)' }}
      />
    </div>
  )
}

/** The editors whose choices come from lists the page loads. */
export interface ProjectPlanOptionEditors {
  type: CellEditor<ProjectPlanNodeDto>
  status: CellEditor<ProjectPlanNodeDto>
  priority: CellEditor<ProjectPlanNodeDto>
  assignees: CellEditor<ProjectPlanNodeDto>
}

interface ProjectPlanTableColumnsParams {
  /** The user may change the plan: row menus, drag handles, add buttons. */
  canManageTasks: boolean
  handleEditTask: (task: ProjectPlanNodeDto) => void
  handleDeleteTask: (task: ProjectPlanNodeDto) => void
  handleEditStage?: (stage: ProjectPlanNodeDto) => void
  openPlanItemDrawer?: (taskId: string) => void
  isDragEnabled?: boolean
  enableDragAndDrop?: boolean
  addDraftTaskAsChild?: (parentId: string) => void
  /** No other new row is waiting to be created. */
  canCreateTasks?: boolean
  taskTypeFilterOptions?: FilterOption[]
  taskStatusFilterOptions?: FilterOption[]
  taskPriorityFilterOptions?: FilterOption[]
  /** Editors built from the page's option lists; without them those
   *  columns are read-only. */
  optionEditors?: ProjectPlanOptionEditors
  /** The project's deliverable names by id, for the Deliverables column. */
  deliverableNames?: ReadonlyMap<string, string>
}

/** The names of the deliverables a plan task serves, in the order the task lists them. */
const deliverableNamesOf = (
  task: ProjectPlanNodeDto,
  deliverableNames: ReadonlyMap<string, string>,
) =>
  (task.deliverableIds ?? [])
    .map((id) => deliverableNames.get(id))
    .filter((name): name is string => !!name)

/**
 * The project plan's columns. Cells render read-only; a column with an
 * editor is edited in place by the grid when the grid has `onCommit`.
 */
export const getProjectPlanTableColumns = ({
  canManageTasks,
  handleEditTask,
  handleDeleteTask,
  handleEditStage,
  openPlanItemDrawer,
  isDragEnabled = false,
  enableDragAndDrop = false,
  addDraftTaskAsChild,
  canCreateTasks = true,
  taskTypeFilterOptions = [],
  taskStatusFilterOptions = [],
  taskPriorityFilterOptions = [],
  optionEditors,
  deliverableNames = new Map(),
}: ProjectPlanTableColumnsParams): ColumnDef<ProjectPlanNodeDto>[] => {
  return [
    ...(canManageTasks
      ? [
          {
            id: 'actions',
            header: '',
            size: enableDragAndDrop ? 64 : 36,
            enableSorting: false,
            enableGlobalFilter: false,
            enableColumnFilter: false,
            enableResizing: false,
            meta: { enableExport: false } satisfies WaydGridColumnMeta,
            cell: ({ row }: { row: any }) => {
              const isDraft = row.original.id.startsWith('draft-')
              const isStage = isStageRow(row.original)

              if (isStage) {
                // Stage rows: no drag handle, show edit and add task options
                const stageMenuItems = []
                if (handleEditStage) {
                  stageMenuItems.push({
                    key: 'edit-stage',
                    label: 'Edit',
                    onClick: () => handleEditStage(row.original),
                  })
                }
                if (canCreateTasks && addDraftTaskAsChild) {
                  stageMenuItems.push({
                    key: 'add-task',
                    label: 'Add Task',
                    onClick: () => addDraftTaskAsChild(row.original.id),
                  })
                }
                return stageMenuItems.length > 0 ? (
                  <Flex align="center" gap={4}>
                    <Dropdown
                      menu={{ items: stageMenuItems }}
                      trigger={['click']}
                    >
                      <Button
                        type="text"
                        size="small"
                        icon={<MoreOutlined />}
                        tabIndex={-1}
                        aria-label="Row actions"
                        data-row-actions=""
                      />
                    </Dropdown>
                  </Flex>
                ) : null
              }

              return (
                <Flex align="center" gap={4}>
                  {enableDragAndDrop && (
                    <DragHandleCell isDragEnabled={isDragEnabled} />
                  )}
                  {!isDraft && (
                    <Dropdown
                      menu={{
                        items: [
                          {
                            key: 'edit',
                            label: 'Edit',
                            onClick: () => handleEditTask(row.original),
                          },
                          ...(canCreateTasks &&
                          addDraftTaskAsChild &&
                          !isMilestoneRow(row.original)
                            ? [
                                {
                                  key: 'add-child',
                                  label: 'Add Child Task',
                                  onClick: () =>
                                    addDraftTaskAsChild(row.original.id),
                                },
                              ]
                            : []),
                          { key: 'divider', type: 'divider' as const },
                          {
                            key: 'delete',
                            label: 'Delete',
                            danger: true,
                            onClick: () => handleDeleteTask(row.original),
                          },
                        ],
                      }}
                      trigger={['click']}
                    >
                      <Button
                        type="text"
                        size="small"
                        icon={<MoreOutlined />}
                        tabIndex={-1}
                        aria-label="Row actions"
                        data-row-actions=""
                      />
                    </Dropdown>
                  )}
                </Flex>
              )
            },
          } as ColumnDef<ProjectPlanNodeDto>,
        ]
      : []),
    {
      accessorKey: 'wbs',
      header: 'WBS',
      size: 100,
      enableGlobalFilter: true,
      enableColumnFilter: true,
      sortFn: 'alphanumeric',
    },
    {
      accessorKey: 'key',
      header: 'Key',
      size: 140,
      enableColumnFilter: true,
      sortFn: 'alphanumeric',
      cell: ({ row, getValue }) => {
        const task = row.original as ProjectPlanNodeDto
        const isDraft = task.id.startsWith('draft-')
        const key = (getValue() as string | undefined) ?? ''

        if (!key || isDraft || !openPlanItemDrawer) {
          return key
        }

        return (
          <Button
            type="link"
            size="small"
            tabIndex={-1}
            style={{ padding: 0, height: 'auto' }}
            onClick={(e) => {
              e.stopPropagation()
              openPlanItemDrawer(task.id)
            }}
          >
            {key}
          </Button>
        )
      },
    },
    {
      accessorKey: 'name',
      header: 'Name',
      size: 300,
      enableColumnFilter: true,
      sortFn: 'alphanumeric',
      meta: {
        treeColumn: true,
        editor: planNameEditor,
      } satisfies WaydGridColumnMeta,
      cell: ({ row }: { row: any }) => {
        const depth = row.depth
        const task = row.original as ProjectPlanNodeDto
        const isDraft = task.id.startsWith('draft-')
        const isStage = isStageRow(task)

        return (
          <Flex
            className={styles.nameCell}
            align="center"
            justify="space-between"
            gap={4}
            style={{ width: '100%' }}
          >
            <Flex align="center" gap={0} style={{ flex: 1, minWidth: 0 }}>
              {Array.from({ length: depth }).map((_, index) => (
                <span key={index} className={styles.indentSpacer} />
              ))}
              {row.getCanExpand() ? (
                <Button
                  type="text"
                  size="small"
                  icon={
                    row.getIsExpanded() ? (
                      <CaretDownOutlined />
                    ) : (
                      <CaretRightOutlined />
                    )
                  }
                  onClick={(e) => {
                    e.stopPropagation()
                    row.getToggleExpandedHandler()()
                  }}
                  tabIndex={-1}
                  aria-expanded={row.getIsExpanded()}
                  aria-label="Show children"
                  className={styles.expanderBtn}
                />
              ) : (
                <span className={styles.indentSpacer} />
              )}
              {isStage ? (
                <span className={styles.stageName}>{task.name}</span>
              ) : (
                <span style={NAME_TEXT_STYLE}>
                  {task.name || (isDraft ? '(New Task)' : '')}
                </span>
              )}
            </Flex>
            {canManageTasks &&
              !isDraft &&
              !isMilestoneRow(task) &&
              addDraftTaskAsChild && (
                <Button
                  type="text"
                  size="small"
                  icon={<PlusOutlined />}
                  tabIndex={-1}
                  className={`${styles.rowActionBtn} ${styles.inlineRowAction}`}
                  onClick={(e) => {
                    e.stopPropagation()
                    addDraftTaskAsChild(task.id)
                  }}
                  disabled={!canCreateTasks}
                  title="Add child task"
                />
              )}
          </Flex>
        )
      },
    },
    {
      id: 'type',
      accessorFn: (row) => row.type?.name ?? row.nodeType ?? '',
      header: 'Type',
      size: 110,
      enableGlobalFilter: true,
      enableColumnFilter: true,
      sortFn: 'text',
      meta: {
        filterType: 'select',
        filterOptions: taskTypeFilterOptions,
        editor: optionEditors?.type,
      } satisfies WaydGridColumnMeta,
      cell: ({ row }: { row: any }) => {
        const task = row.original as ProjectPlanNodeDto
        return task.type?.name ?? task.nodeType ?? ''
      },
    },
    {
      id: 'status',
      accessorFn: (row) => row.status?.name ?? '',
      header: 'Status',
      size: 130,
      enableGlobalFilter: true,
      enableColumnFilter: true,
      sortFn: 'text',
      meta: {
        filterType: 'select',
        filterOptions: taskStatusFilterOptions,
        editor: optionEditors?.status,
      } satisfies WaydGridColumnMeta,
      cell: (info) => {
        const status = (info.getValue() as string) ?? ''
        return (
          <Tag color={statusTagColors[status] || 'default'}>
            {status || '-'}
          </Tag>
        )
      },
    },
    {
      id: 'priority',
      accessorFn: (row) => row.priority?.name ?? '',
      header: 'Priority',
      size: 110,
      enableGlobalFilter: true,
      enableColumnFilter: true,
      sortFn: 'text',
      meta: {
        filterType: 'select',
        filterOptions: taskPriorityFilterOptions,
        editor: optionEditors?.priority,
      } satisfies WaydGridColumnMeta,
      cell: (info) => {
        const task = info.row.original as ProjectPlanNodeDto
        const priority = (info.getValue() as string) ?? ''
        if (!priority) return isStageRow(task) ? null : '-'
        // Plain text: Status and Schedule already carry tags, and a third
        // colored column on every row is more color than the grid can spend.
        return priority
      },
    },
    {
      id: 'schedule',
      accessorFn: (row) => getPlanScheduleLabel(row) ?? '',
      header: 'Schedule',
      size: 120,
      enableGlobalFilter: true,
      enableColumnFilter: true,
      sortFn: 'text',
      meta: {
        filterType: 'select',
        filterOptions: [
          { label: 'Overdue', value: 'Overdue' },
          { label: 'Due This Week', value: 'Due This Week' },
          { label: 'Upcoming', value: 'Upcoming' },
        ],
      } satisfies WaydGridColumnMeta,
      cell: (info) => {
        // Derived from the dates, so there is nothing here to edit.
        const label = info.getValue() as string
        if (!label) return null

        return <Tag color={scheduleTagColors[label]}>{label}</Tag>
      },
    },
    {
      id: 'plannedStart',
      accessorFn: (row) =>
        formatDate(isMilestoneRow(row) ? row.plannedDate : row.start),
      header: 'Planned Start',
      size: 130,
      enableGlobalFilter: true,
      enableColumnFilter: true,
      meta: {
        filterType: 'date',
        editor: planStartEditor,
        exportFormatter: (_value, row) =>
          formatDate(isMilestoneRow(row) ? row.plannedDate : row.start),
      } satisfies WaydGridColumnMeta,
      sortFn: dateSortBy((row: any) =>
        isMilestoneRow(row.original)
          ? row.original.plannedDate
          : row.original.start,
      ),
      cell: (info) => {
        const task = info.row.original as ProjectPlanNodeDto
        const value = (info.getValue() as string) ?? ''
        // A milestone's due date lives here, so it carries the overdue
        // treatment that the Planned End column gives every other item.
        return isMilestoneRow(task) && isPlanItemOverdue(task) ? (
          <span style={overdueDateStyle}>{value}</span>
        ) : (
          value
        )
      },
    },
    {
      id: 'plannedEnd',
      accessorFn: (row) => formatDate(row.end),
      header: 'Planned End',
      size: 130,
      enableGlobalFilter: true,
      enableColumnFilter: true,
      meta: {
        filterType: 'date',
        editor: planEndEditor,
        exportFormatter: (_value, row) => formatDate(row.end),
      } satisfies WaydGridColumnMeta,
      sortFn: dateSortBy((row: any) => row.original.end),
      cell: (info) => {
        const task = info.row.original as ProjectPlanNodeDto
        const value = (info.getValue() as string) ?? ''
        return isPlanItemOverdue(task) ? (
          <span style={overdueDateStyle}>{value}</span>
        ) : (
          value
        )
      },
    },
    {
      id: 'assignees',
      accessorFn: (row) => row.assignees?.map((a) => a.name).join(', ') ?? '',
      header: 'Assignees',
      size: 250,
      enableGlobalFilter: true,
      enableColumnFilter: true,
      sortFn: 'text',
      meta: { editor: optionEditors?.assignees } satisfies WaydGridColumnMeta,
      cell: (info) => {
        const assignees = info.row.original.assignees ?? []
        if (assignees.length === 0) return ''
        return <TagListCell values={assignees.map((a) => a.name)} />
      },
    },
    {
      // Read-only here: a task's deliverables are set in its edit form.
      id: 'deliverables',
      accessorFn: (row) => deliverableNamesOf(row, deliverableNames).join(', '),
      header: 'Deliverables',
      size: 220,
      enableGlobalFilter: true,
      enableColumnFilter: true,
      sortFn: 'text',
      cell: (info) => {
        const names = deliverableNamesOf(info.row.original, deliverableNames)
        return names.length === 0 ? '' : <TagListCell values={names} />
      },
    },
    {
      id: 'progress',
      accessorFn: (row) => row.progress ?? undefined,
      header: 'Progress',
      size: 90,
      enableGlobalFilter: true,
      enableColumnFilter: true,
      meta: {
        filterType: 'numericRange',
        filterPlaceholder: 'e.g. >=10 or 2-6',
        editor: planProgressEditor,
      } satisfies WaydGridColumnMeta,
      cell: (info) => {
        const task = info.row.original as ProjectPlanNodeDto
        return task.progress !== undefined ? `${task.progress} %` : ''
      },
      sortFn: 'basic',
      sortUndefined: -1,
    },
    {
      id: 'estimatedEffortHours',
      accessorFn: (row) => row.estimatedEffortHours ?? undefined,
      header: 'Est Effort (hrs)',
      size: 90,
      enableGlobalFilter: true,
      enableColumnFilter: true,
      meta: {
        filterType: 'numericRange',
        filterPlaceholder: 'e.g. >=10 or 2-6',
        editor: planEffortEditor,
      } satisfies WaydGridColumnMeta,
      cell: (info) => info.row.original.estimatedEffortHours,
      sortFn: 'basic',
      sortUndefined: -1,
    },
  ]
}

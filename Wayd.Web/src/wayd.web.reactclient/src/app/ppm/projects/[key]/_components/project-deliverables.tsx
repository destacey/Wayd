'use client'

import ChangeProjectDeliverableStatusForm from '@/src/app/ppm/projects/_components/change-project-deliverable-status-form'
import DeleteProjectDeliverableForm from '@/src/app/ppm/projects/_components/delete-project-deliverable-form'
import DeliverableProgress from '@/src/app/ppm/projects/_components/deliverable-progress'
import SetDeliverableTasksForm, {
  linkedTasks,
} from '@/src/app/ppm/projects/_components/set-deliverable-tasks-form'
import { deliverableErrorMessage } from '@/src/app/ppm/projects/_components/project-deliverable-errors'
import ProjectDeliverableForm from '@/src/app/ppm/projects/_components/project-deliverable-form'
import {
  ListDetailLayout,
  useSelectedRecord,
} from '@/src/components/common/list-detail'
import { LabeledContent } from '@/src/components/common/content'
import { MarkdownRenderer } from '@/src/components/common/markdown'
import {
  StatusHistoryTag,
  StatusHistoryTimeline,
} from '@/src/components/common/status-workflows'
import {
  DragHandleCell,
  WaydGrid,
  type GridColumnContext,
  type RowReorderEvent,
} from '@/src/components/common/wayd-grid'
import {
  createActionsColumn,
  type ColumnDef,
} from '@/src/components/common/wayd-grid-core'
import { useMessage } from '@/src/components/contexts/messaging'
import {
  ProjectDeliverableDto,
  ProjectDetailsDto,
  StatusCategory,
} from '@/src/services/wayd-api'
import {
  useGetProjectDeliverableStatusHistoryQuery,
  useGetProjectDeliverablesQuery,
  useReorderProjectDeliverablesMutation,
} from '@/src/store/features/ppm/project-deliverables-api'
import { useGetProjectPlanTreeQuery } from '@/src/store/features/ppm/projects-api'
import { toApiError } from '@/src/utils'
import { formatCalendarDate } from '@/src/utils/calendar-date'
import { Card, Flex, Switch, Tag, Typography } from 'antd'
import type { ItemType } from 'antd/es/menu/interface'
import Link from 'next/link'
import { useState } from 'react'
import { mergeDeliverableOrder } from './deliverable-order'
import { isOverdue } from './deliverable-status'

const { Text } = Typography

export interface ProjectDeliverablesProps {
  project: ProjectDetailsDto
  /** The caller holds Update and leads the project, and the project is still open. */
  canManage: boolean
  /** The caller holds Update. Linking plan tasks is plan work, which needs no project role. */
  canLinkTasks: boolean
  /**
   * The project is being refetched. Every change sends the project's revision
   * back, so a change made before the new revision arrives would be refused.
   */
  projectRefreshing: boolean
  /** Raised by the section's Add Deliverable action, which the page renders. */
  addRequested: boolean
  onAddClosed: () => void
}

/** Deliverables can be deleted only before the project starts, and only before work on them starts. */
const canDelete = (
  project: ProjectDetailsDto,
  deliverable: ProjectDeliverableDto,
) =>
  (project.status.name === 'Proposed' || project.status.name === 'Approved') &&
  deliverable.status.category === StatusCategory.Proposed

type Dialog = 'edit' | 'status' | 'delete' | 'tasks'

const Count = ({
  label,
  value,
  tone,
}: {
  label: string
  value: number
  tone?: 'warning'
}) => (
  <Card size="small" style={{ flex: '1 1 140px' }}>
    <Flex vertical gap={2}>
      <Text type="secondary">{label}</Text>
      <Text
        strong
        style={{ fontSize: 22 }}
        type={tone === 'warning' && value > 0 ? 'warning' : undefined}
      >
        {value}
      </Text>
    </Flex>
  </Card>
)

const OwnerLink = ({ owner }: { owner: ProjectDeliverableDto['owner'] }) =>
  owner ? (
    <Link href={`/organizations/employees/${owner.key}`}>{owner.name}</Link>
  ) : null

/**
 * What the project has committed to produce: a count of where they stand, the
 * deliverables in their order, and — once one is opened — that deliverable in
 * full in a side panel (a drawer on narrow screens).
 *
 * The grid's order is the deliverables' order, so dragging a row reorders
 * them. Progress is derived from the plan tasks linked to each one. Descoped deliverables are hidden until asked for, since they no longer
 * count toward the project being done; reordering the rest leaves them where
 * they were.
 */
const ProjectDeliverables = ({
  project,
  canManage,
  canLinkTasks,
  projectRefreshing,
  addRequested,
  onAddClosed,
}: ProjectDeliverablesProps) => {
  const messageApi = useMessage()
  const { selectedId, select, clear } = useSelectedRecord()

  const { data, isLoading, isFetching, refetch } =
    useGetProjectDeliverablesQuery(project.id)
  const [reorder, { isLoading: isReordering }] =
    useReorderProjectDeliverablesMutation()
  const { data: plan = [] } = useGetProjectPlanTreeQuery(project.key, {
    skip: !project.projectLifecycle,
  })

  const [showDescoped, setShowDescoped] = useState(false)
  const [active, setActive] = useState<{
    dialog: Dialog
    target: ProjectDeliverableDto
  } | null>(null)
  // Keyed by deliverable, so the history closes when another one is opened.
  const [historyFor, setHistoryFor] = useState<string | null>(null)

  // A change sent before the last one's new project revision arrives would be refused as stale.
  const isBusy = projectRefreshing || isFetching || isReordering

  const deliverables = data ?? []
  const descoped = deliverables.filter(
    (d) => d.status.category === StatusCategory.Removed,
  )
  const accepted = deliverables.filter(
    (d) => d.status.category === StatusCategory.Done,
  )
  const open = deliverables.filter((d) => d.isOpen)
  const overdue = open.filter(isOverdue)

  const rows = showDescoped
    ? deliverables
    : deliverables.filter((d) => d.status.category !== StatusCategory.Removed)

  const selected = deliverables.find((d) => d.id === selectedId) ?? null
  const selectedTasks = selected ? linkedTasks(plan, selected.id) : []
  const showHistory = !!selected && historyFor === selected.id

  const { data: history, isLoading: historyLoading } =
    useGetProjectDeliverableStatusHistoryQuery(
      { projectId: project.id, deliverableId: selected?.id ?? '' },
      { skip: !selected || !showHistory },
    )

  const getActionItems = (deliverable: ProjectDeliverableDto): ItemType[] => {
    const openDialog = (dialog: Dialog) => () =>
      setActive({ dialog, target: deliverable })
    // Links do not move the project's revision, so they wait on nothing.
    // A descoped deliverable takes no new work, matching the task form's picker.
    const linkItems: ItemType[] =
      canLinkTasks &&
      project.projectLifecycle &&
      deliverable.status.category !== StatusCategory.Removed
        ? [
            {
              key: 'tasks',
              label: 'Link Tasks',
              onClick: openDialog('tasks'),
            },
          ]
        : []
    if (!canManage) return linkItems
    return [
      ...linkItems,
      {
        key: 'status',
        label: 'Change Status',
        disabled: isBusy,
        onClick: openDialog('status'),
      },
      {
        key: 'edit',
        label: 'Edit',
        disabled: isBusy,
        onClick: openDialog('edit'),
      },
      ...(canDelete(project, deliverable)
        ? [
            { key: 'delete-divider', type: 'divider' as const },
            {
              key: 'delete',
              label: 'Delete',
              danger: true,
              disabled: isBusy,
              onClick: openDialog('delete'),
            },
          ]
        : []),
    ]
  }

  const onRowReorder = async (
    event: RowReorderEvent<ProjectDeliverableDto>,
  ) => {
    if (isBusy) return
    const ordered = mergeDeliverableOrder(
      deliverables.map((d) => d.id),
      new Set(showDescoped ? [] : descoped.map((d) => d.id)),
      event.orderedData.map((d) => d.id),
    )
    try {
      await reorder({
        projectId: project.id,
        deliverableIds: ordered,
        expectedRevision: project.revision,
      }).unwrap()
    } catch (error) {
      messageApi.error(
        deliverableErrorMessage(
          toApiError(error),
          'Failed to reorder the deliverables.',
        ),
      )
      refetch()
    }
  }

  const columns = (
    context: GridColumnContext,
  ): ColumnDef<ProjectDeliverableDto, any>[] => [
    // The grab handle shares the ⋯ cell, so one column holds everything that acts on a row.
    createActionsColumn<ProjectDeliverableDto>({
      getItems: getActionItems,
      ariaLabel: 'Deliverable actions',
      unavailable: !canManage && !canLinkTasks,
      size: 70,
      // Only those who can reorder get a handle; anyone else sees just the ⋯.
      leading: canManage ? (
        <DragHandleCell
          isDragEnabled={context.isDragEnabled && !isBusy}
          disabledTooltip={
            isBusy
              ? 'Saving the last change.'
              : 'Clear sorting, filters, and search to reorder deliverables.'
          }
        />
      ) : undefined,
    }),
    {
      id: 'order',
      accessorKey: 'order',
      header: '#',
      size: 60,
      enableColumnFilter: false,
    },
    { id: 'name', accessorKey: 'name', header: 'Deliverable', size: 280 },
    {
      id: 'status',
      accessorKey: 'status.name',
      header: 'Status',
      size: 140,
      meta: { filterType: 'set' },
      cell: ({ row }) => <StatusHistoryTag status={row.original.status} />,
    },
    {
      id: 'progress',
      accessorKey: 'progress',
      header: 'Progress',
      size: 150,
      meta: {
        filterType: 'numericRange',
        filterPlaceholder: 'e.g. >=50',
      },
      cell: ({ row }) => (
        <DeliverableProgress progress={row.original.progress} width={120} />
      ),
    },
    {
      id: 'owner',
      accessorKey: 'owner.name',
      header: 'Owner',
      size: 180,
      cell: ({ row }) => <OwnerLink owner={row.original.owner} />,
    },
    {
      id: 'targetDate',
      accessorKey: 'targetDate',
      header: 'Target Date',
      size: 170,
      meta: { columnType: 'dateOnly' },
      cell: ({ row }) => (
        <Flex gap={6} align="center">
          {formatCalendarDate(row.original.targetDate)}
          {isOverdue(row.original) && <Tag color="warning">Overdue</Tag>}
        </Flex>
      ),
    },
    {
      id: 'acceptedOn',
      accessorKey: 'acceptedOn',
      header: 'Accepted On',
      size: 160,
      meta: { columnType: 'dateTime' },
    },
  ]

  const details = selected && (
    <Flex vertical gap={12}>
      <Flex gap="small" align="center" wrap>
        <StatusHistoryTag
          status={selected.status}
          onOpenHistory={() => setHistoryFor(showHistory ? null : selected.id)}
        />
        {isOverdue(selected) && <Tag color="warning">Overdue</Tag>}
      </Flex>
      <LabeledContent label="Progress">
        <DeliverableProgress progress={selected.progress} width={200} />
      </LabeledContent>
      <LabeledContent label="Linked Tasks">
        {selectedTasks.length === 0 ? (
          <Text type="secondary">None</Text>
        ) : (
          <Flex vertical gap={2}>
            {selectedTasks.map((t) => (
              <Text key={t.id}>
                <Text type="secondary">{t.key}</Text> {t.name}
              </Text>
            ))}
          </Flex>
        )}
      </LabeledContent>
      <LabeledContent label="Owner">
        {selected.owner ? (
          <OwnerLink owner={selected.owner} />
        ) : (
          <Text type="secondary">Not named</Text>
        )}
      </LabeledContent>
      <LabeledContent label="Target Date">
        {selected.targetDate ? (
          formatCalendarDate(selected.targetDate)
        ) : (
          <Text type="secondary">Not set</Text>
        )}
      </LabeledContent>
      {selected.acceptedBy && (
        <LabeledContent label="Accepted By">
          {selected.acceptedBy.name}
        </LabeledContent>
      )}
      {selected.statusReason && (
        <LabeledContent label="Reason">{selected.statusReason}</LabeledContent>
      )}
      <LabeledContent label="Description">
        {selected.description ? (
          <MarkdownRenderer markdown={selected.description} />
        ) : (
          <Text type="secondary">Not provided</Text>
        )}
      </LabeledContent>
      <LabeledContent label="Acceptance Criteria">
        {selected.acceptanceCriteria ? (
          <MarkdownRenderer markdown={selected.acceptanceCriteria} />
        ) : (
          <Text type="secondary">Not provided</Text>
        )}
      </LabeledContent>
      {showHistory && (
        <LabeledContent label="Status History">
          <StatusHistoryTimeline
            transitions={history}
            isLoading={historyLoading}
            emptyDescription="This deliverable has no status changes yet."
          />
        </LabeledContent>
      )}
    </Flex>
  )

  const close = () => setActive(null)

  return (
    <Flex vertical gap="middle">
      <Flex gap="small" wrap>
        <Count label="Open" value={open.length} />
        <Count label="Overdue" value={overdue.length} tone="warning" />
        <Count label="Accepted" value={accepted.length} />
        <Count label="Descoped" value={descoped.length} />
      </Flex>

      <ListDetailLayout
        open={!!selected}
        onClose={clear}
        title={selected?.name}
        details={details}
        actionItems={selected ? getActionItems(selected) : undefined}
      >
        <WaydGrid
          columns={columns}
          data={rows}
          isLoading={isLoading}
          onRefresh={async () => {
            refetch()
          }}
          persistStateKey="ppm-project-deliverables"
          csvFileName={`${project.key}-deliverables`}
          getRowId={(d) => d.id}
          onRowActivate={(d) => select(d.id)}
          activatedRowId={selected?.id ?? null}
          getRowActivateLabel={(d) => d.name}
          onRowReorder={canManage ? onRowReorder : undefined}
          emptyMessage={
            descoped.length > 0
              ? 'Every deliverable was descoped.'
              : 'No deliverables yet.'
          }
          rightSlot={
            <Flex gap={6} align="center">
              <Switch
                id="show-descoped-deliverables"
                size="small"
                checked={showDescoped}
                onChange={setShowDescoped}
              />
              <label htmlFor="show-descoped-deliverables">
                <Text type="secondary">Show descoped</Text>
              </label>
            </Flex>
          }
        />
      </ListDetailLayout>

      {addRequested && (
        <ProjectDeliverableForm
          projectId={project.id}
          projectRevision={project.revision}
          onFormComplete={onAddClosed}
          onFormCancel={onAddClosed}
        />
      )}
      {active?.dialog === 'edit' && (
        <ProjectDeliverableForm
          projectId={project.id}
          projectRevision={project.revision}
          deliverable={active.target}
          onFormComplete={close}
          onFormCancel={close}
        />
      )}
      {active?.dialog === 'status' && (
        <ChangeProjectDeliverableStatusForm
          projectId={project.id}
          projectRevision={project.revision}
          deliverable={active.target}
          onFormComplete={close}
          onFormCancel={close}
        />
      )}
      {active?.dialog === 'tasks' && (
        <SetDeliverableTasksForm
          projectId={project.id}
          deliverable={active.target}
          plan={plan}
          onFormComplete={close}
          onFormCancel={close}
        />
      )}
      {active?.dialog === 'delete' && (
        <DeleteProjectDeliverableForm
          projectId={project.id}
          projectRevision={project.revision}
          deliverable={active.target}
          onFormComplete={() => {
            if (active.target.id === selectedId) clear()
            close()
          }}
          onFormCancel={close}
        />
      )}
    </Flex>
  )
}

export default ProjectDeliverables

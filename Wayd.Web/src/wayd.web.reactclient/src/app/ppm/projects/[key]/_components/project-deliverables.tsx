'use client'

import ChangeProjectDeliverableStatusForm from '@/src/app/ppm/projects/_components/change-project-deliverable-status-form'
import DeleteProjectDeliverableForm from '@/src/app/ppm/projects/_components/delete-project-deliverable-form'
import { deliverableErrorMessage } from '@/src/app/ppm/projects/_components/project-deliverable-errors'
import ProjectDeliverableForm from '@/src/app/ppm/projects/_components/project-deliverable-form'
import { WaydEmpty } from '@/src/components/common'
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
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
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
import { toApiError } from '@/src/utils'
import { formatCalendarDate } from '@/src/utils/calendar-date'
import { Button, Card, Flex, Grid, Switch, Tag, Typography } from 'antd'
import Link from 'next/link'
import { useState } from 'react'
import { mergeDeliverableOrder } from './deliverable-order'
import { isOverdue } from './deliverable-status'

const { Text } = Typography
const { useBreakpoint } = Grid

export interface ProjectDeliverablesProps {
  project: ProjectDetailsDto
  /** The caller holds Update and leads the project, and the project is still open. */
  canManage: boolean
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

/**
 * What the project has committed to produce: a count of where they stand, the
 * deliverables in their order, and the one selected in full — its acceptance
 * criteria, its status history, and what can be done with it.
 *
 * The grid's order is the deliverables' order, so dragging a row reorders
 * them. Descoped deliverables are hidden until asked for, since they no longer
 * count toward the project being done; reordering the rest leaves them where
 * they were.
 */
const ProjectDeliverables = ({
  project,
  canManage,
  projectRefreshing,
  addRequested,
  onAddClosed,
}: ProjectDeliverablesProps) => {
  const messageApi = useMessage()
  const screens = useBreakpoint()

  const { data, isLoading, isFetching, refetch } =
    useGetProjectDeliverablesQuery(project.id)
  const [reorder, { isLoading: isReordering }] =
    useReorderProjectDeliverablesMutation()

  const [showDescoped, setShowDescoped] = useState(false)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [editing, setEditing] = useState<ProjectDeliverableDto | null>(null)
  const [changingStatus, setChangingStatus] =
    useState<ProjectDeliverableDto | null>(null)
  const [deleting, setDeleting] = useState<ProjectDeliverableDto | null>(null)
  // Keyed by deliverable, so the history closes when the selection moves on by itself.
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

  // The first open deliverable until the reader picks one, so the panel is never empty when
  // there is something to show.
  const selected =
    rows.find((d) => d.id === selectedId) ?? open[0] ?? rows[0] ?? null
  const showHistory = !!selected && historyFor === selected.id

  const { data: history, isLoading: historyLoading } =
    useGetProjectDeliverableStatusHistoryQuery(
      { projectId: project.id, deliverableId: selected?.id ?? '' },
      { skip: !selected || !showHistory },
    )

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
    ...(canManage
      ? [
          {
            id: 'drag',
            header: '',
            size: 36,
            enableSorting: false,
            enableColumnFilter: false,
            enableResizing: false,
            enableGlobalFilter: false,
            enableHiding: false,
            meta: { enableReordering: false },
            cell: () => (
              <DragHandleCell
                isDragEnabled={context.isDragEnabled && !isBusy}
                disabledTooltip={
                  isBusy
                    ? 'Saving the last change.'
                    : 'Clear sorting, filters, and search to reorder deliverables.'
                }
              />
            ),
          } as ColumnDef<ProjectDeliverableDto, any>,
        ]
      : []),
    { id: 'order', accessorKey: 'order', header: '#', size: 56 },
    { id: 'name', accessorKey: 'name', header: 'Deliverable', size: 300 },
    {
      id: 'status',
      accessorKey: 'status.name',
      header: 'Status',
      size: 140,
      meta: { filterType: 'set' },
      cell: ({ row }) => <StatusHistoryTag status={row.original.status} />,
    },
    {
      id: 'owner',
      accessorKey: 'owner.name',
      header: 'Owner',
      size: 180,
      cell: ({ row }) =>
        row.original.owner ? (
          <Link href={`/organizations/employees/${row.original.owner.key}`}>
            {row.original.owner.name}
          </Link>
        ) : null,
    },
    {
      id: 'targetDate',
      accessorKey: 'targetDate',
      header: 'Target Date',
      size: 160,
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
      size: 150,
      meta: { columnType: 'dateTime' },
    },
  ]

  return (
    <Flex vertical gap="middle">
      <Flex gap="small" wrap>
        <Count label="Open" value={open.length} />
        <Count label="Overdue" value={overdue.length} tone="warning" />
        <Count label="Accepted" value={accepted.length} />
        <Count label="Descoped" value={descoped.length} />
      </Flex>

      <Flex gap="middle" vertical={!screens.lg} align="start">
        {/* A plain block around the grid: as a flex item itself it would collapse to its toolbar. */}
        <div style={{ flex: 1, minWidth: 0, width: '100%' }}>
          <WaydGrid
            variant="simple"
            columns={columns}
            data={rows}
            isLoading={isLoading}
            onRefresh={() => refetch()}
            getRowId={(d) => d.id}
            onRowActivate={(d) => setSelectedId(d.id)}
            activatedRowId={selected?.id ?? null}
            getRowActivateLabel={(d) => d.name}
            onRowReorder={canManage ? onRowReorder : undefined}
            emptyMessage="No deliverables yet."
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
        </div>

        <Card
          size="small"
          style={{ width: screens.lg ? 380 : '100%', flex: 'none' }}
          title={selected?.name ?? 'Deliverable'}
        >
          {!selected ? (
            <WaydEmpty message="Add the deliverables this project has committed to." />
          ) : (
            <Flex vertical gap={12}>
              <Flex gap="small" align="center" wrap>
                <StatusHistoryTag
                  status={selected.status}
                  onOpenHistory={() =>
                    setHistoryFor(showHistory ? null : selected.id)
                  }
                />
                {isOverdue(selected) && <Tag color="warning">Overdue</Tag>}
              </Flex>
              <Flex gap="large" wrap>
                <LabeledContent label="Owner">
                  {selected.owner ? (
                    <Link
                      href={`/organizations/employees/${selected.owner.key}`}
                    >
                      {selected.owner.name}
                    </Link>
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
              </Flex>
              {selected.statusReason && (
                <LabeledContent label="Reason">
                  {selected.statusReason}
                </LabeledContent>
              )}
              <LabeledContent label="Acceptance Criteria">
                {selected.acceptanceCriteria ? (
                  <MarkdownRenderer markdown={selected.acceptanceCriteria} />
                ) : (
                  <Text type="secondary">Not provided</Text>
                )}
              </LabeledContent>
              {selected.description && (
                <LabeledContent label="Description">
                  <MarkdownRenderer markdown={selected.description} />
                </LabeledContent>
              )}
              {canManage && (
                <Flex gap="small" wrap>
                  <Button
                    type="primary"
                    disabled={isBusy}
                    onClick={() => setChangingStatus(selected)}
                  >
                    Change Status
                  </Button>
                  <Button
                    disabled={isBusy}
                    onClick={() => setEditing(selected)}
                  >
                    Edit
                  </Button>
                  {canDelete(project, selected) && (
                    <Button
                      danger
                      disabled={isBusy}
                      onClick={() => setDeleting(selected)}
                    >
                      Delete
                    </Button>
                  )}
                </Flex>
              )}
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
          )}
        </Card>
      </Flex>

      {addRequested && (
        <ProjectDeliverableForm
          projectId={project.id}
          projectRevision={project.revision}
          onFormComplete={onAddClosed}
          onFormCancel={onAddClosed}
        />
      )}
      {editing && (
        <ProjectDeliverableForm
          projectId={project.id}
          projectRevision={project.revision}
          deliverable={editing}
          onFormComplete={() => setEditing(null)}
          onFormCancel={() => setEditing(null)}
        />
      )}
      {changingStatus && (
        <ChangeProjectDeliverableStatusForm
          projectId={project.id}
          projectRevision={project.revision}
          deliverable={changingStatus}
          onFormComplete={() => setChangingStatus(null)}
          onFormCancel={() => setChangingStatus(null)}
        />
      )}
      {deleting && (
        <DeleteProjectDeliverableForm
          projectId={project.id}
          projectRevision={project.revision}
          deliverable={deleting}
          onFormComplete={() => {
            setDeleting(null)
            setSelectedId(null)
          }}
          onFormCancel={() => setDeleting(null)}
        />
      )}
    </Flex>
  )
}

export default ProjectDeliverables

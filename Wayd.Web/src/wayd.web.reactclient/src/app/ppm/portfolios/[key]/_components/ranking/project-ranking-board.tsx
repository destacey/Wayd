'use client'

import { WaydGrid, WaydTooltip } from '@/src/components/common'
import { LifecycleStatusTagCellRenderer } from '@/src/components/common/wayd-grid-cell-renderers'
import { useMessage } from '@/src/components/contexts/messaging'
import ProjectDrawer from '@/src/app/ppm/_components/project-drawer'
import { ProjectListDto } from '@/src/services/wayd-api'
import { useMovePortfolioProjectRanksMutation } from '@/src/store/features/ppm/portfolios-api'
import { isApiError } from '@/src/utils'
import { HolderOutlined, MoreOutlined } from '@ant-design/icons'
import {
  ColDef,
  GetRowIdParams,
  ICellRendererParams,
  RowDragEndEvent,
  RowSelectionOptions,
} from 'ag-grid-community'
import { AgGridReact } from 'ag-grid-react'
import { Button, Dropdown, Flex, Tag, theme } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import Link from 'next/link'
import { MouseEvent, useCallback, useMemo, useRef, useState } from 'react'
import styles from './project-ranking-board.module.css'
import { buildMoveRanksPayload, RankableRow } from './ranking'

export interface ProjectRankingBoardProps {
  portfolioId: string
  portfolioKey: number
  projects: ProjectListDto[]
  /** Whether the portfolio has a scoring model assigned (drives the optional score column). */
  scoringEnabled: boolean
  /** Whether the current user may rank (portfolio Update permission). */
  canManage: boolean
  isLoading?: boolean
  refetch: () => void
}

interface DragHandleCellProps extends ICellRendererParams<ProjectListDto> {
  /** Whether the user may rank projects at all. */
  canRank: boolean
  /** Whether dragging is currently allowed (false while sorted/filtered/searched). */
  dragEnabled: boolean
  menuItems: ItemType[]
}

const stopRowClick = (event: MouseEvent) => {
  event.stopPropagation()
}

// Registers a custom element as ag-grid's row drag source (managed row drag). The handle is always
// shown, but rendered disabled (and not registered as a drag source) while the list isn't in pure
// rank order; sorting/filtering/searching would make a drop ambiguous.
const ActionsCellRenderer = (props: DragHandleCellProps) => {
  const { token } = theme.useToken()
  const dragHandleRef = useCallback(
    (node: HTMLSpanElement | null) => {
      if (node && props.dragEnabled) {
        props.registerRowDragger(node)
      }
    },
    [props],
  )
  const showMenu = props.menuItems.length > 0
  const showDragHandle = props.canRank

  return (
    <Flex align="center" gap={2} style={{ height: '100%' }}>
      {showDragHandle && (
        <WaydTooltip
          title={
            props.dragEnabled
              ? undefined
              : 'Clear sorting, filters, and search to enable ranking.'
          }
        >
          <span
            ref={dragHandleRef}
            style={{
              cursor: props.dragEnabled ? 'grab' : 'not-allowed',
              color: props.dragEnabled
                ? token.colorTextTertiary
                : token.colorTextDisabled,
              display: 'inline-flex',
              padding: '0 4px',
            }}
            aria-label="Drag to reorder"
            aria-disabled={!props.dragEnabled}
          >
            <HolderOutlined />
          </span>
        </WaydTooltip>
      )}
      {showMenu && (
        <Dropdown menu={{ items: props.menuItems }} trigger={['click']}>
          <Button
            type="text"
            size="small"
            icon={<MoreOutlined />}
            onClick={stopRowClick}
            onMouseDown={stopRowClick}
          />
        </Dropdown>
      )}
    </Flex>
  )
}

const ProjectNameCellRenderer = (
  props: ICellRendererParams<ProjectListDto>,
) => {
  if (!props.data) return null
  return (
    <Link href={`/ppm/projects/${props.data.key}`} onClick={stopRowClick}>
      {props.data.name}
    </Link>
  )
}

const ScoreCellRenderer = (props: ICellRendererParams<ProjectListDto>) => {
  const score = props.data?.currentScore
  return (
    <Tag color={score ? 'blue' : 'default'}>
      {score ? score.value.toFixed(2) : 'Unscored'}
    </Tag>
  )
}

const rowSelection: RowSelectionOptions<ProjectListDto> = {
  mode: 'multiRow',
  checkboxes: false,
  headerCheckbox: false,
  enableClickSelection: true,
}

const ProjectRankingBoard = ({
  portfolioId,
  portfolioKey,
  projects,
  scoringEnabled,
  canManage,
  isLoading,
  refetch,
}: ProjectRankingBoardProps) => {
  const messageApi = useMessage()
  const gridRef = useRef<AgGridReact<ProjectListDto>>(null)
  const [moveRanks] = useMovePortfolioProjectRanksMutation()
  const [selectedProjectKey, setSelectedProjectKey] = useState<string | null>(
    null,
  )
  const [drawerOpen, setDrawerOpen] = useState(false)

  // Dragging only makes sense in the API-provided rank order. When the user sorts, column-filters, or
  // uses the global search, the visible order no longer reflects rank, so the drag handle is disabled
  // (shown but not draggable) until all of those are cleared.
  const [interactionActive, setInteractionActive] = useState(false)
  const dragEnabled = canManage && !interactionActive

  const orderedProjects = useMemo(
    () =>
      [...projects].sort((a, b) => {
        const rankComparison = a.rank - b.rank
        return rankComparison === 0
          ? a.name.localeCompare(b.name)
          : rankComparison
      }),
    [projects],
  )

  const getRowId = (params: GetRowIdParams<ProjectListDto>) => params.data.id

  const openProjectDrawer = useCallback((projectKey: string) => {
    setSelectedProjectKey(projectKey)
    setDrawerOpen(true)
  }, [])

  const syncInteractionState = () => {
    const api = gridRef.current?.api
    if (!api) return
    const sorted = api.getColumnState().some((c) => c.sort)
    const filtered = api.isAnyFilterPresent()
    const searched = api.isQuickFilterPresent()
    setInteractionActive(sorted || filtered || searched)
  }

  const onRowDragEnd = async (event: RowDragEndEvent<ProjectListDto>) => {
    if (!dragEnabled) return

    // Reconstruct the post-drop visual order and locate the moved rows, then derive the move payload
    // (moved ids + their ranked neighbours) — the server computes the new fractional ranks.
    const movedIds = new Set(
      event.nodes
        .map((node) => node.data?.id)
        .filter((id): id is string => Boolean(id)),
    )
    if (movedIds.size === 0 && event.node.data?.id) {
      movedIds.add(event.node.data.id)
    }

    const ordered: RankableRow[] = []
    const movedIndices: number[] = []
    event.api.forEachNodeAfterFilterAndSort((node) => {
      if (!node.data) return
      if (movedIds.has(node.data.id)) movedIndices.push(ordered.length)
      ordered.push({ id: node.data.id, rank: node.data.rank })
    })

    const payload = buildMoveRanksPayload(ordered, movedIndices)
    if (!payload) {
      refetch()
      return
    }

    try {
      const response = await moveRanks({
        id: portfolioId,
        request: payload,
        portfolioIdOrKey: portfolioKey.toString(),
      })
      if ('error' in response && response.error) throw response.error
    } catch (error) {
      // Revert the optimistic drag by re-reading the server order.
      refetch()
      messageApi.error(
        (isApiError(error) ? error.detail : undefined) ??
          'An error occurred while reordering projects.',
      )
    }
  }

  const columnDefs = useMemo<ColDef<ProjectListDto>[]>(
    () => [
      {
        width: 70,
        filter: false,
        sortable: false,
        resizable: false,
        suppressHeaderMenuButton: true,
        cellClass: styles.rowActionsCell,
        cellRenderer: (params: ICellRendererParams<ProjectListDto>) => (
          <ActionsCellRenderer
            {...params}
            canRank={canManage}
            dragEnabled={dragEnabled}
            menuItems={
              params.data
                ? [
                    {
                      key: 'view-details',
                      label: 'View Details',
                      onClick: () => openProjectDrawer(params.data!.key),
                    },
                    {
                      key: 'open-project',
                      label: (
                        <Link href={`/ppm/projects/${params.data.key}`}>
                          Open Project
                        </Link>
                      ),
                    },
                  ]
                : []
            }
          />
        ),
      },
      {
        field: 'position',
        headerName: 'Rank',
        width: 90,
        valueFormatter: (p) => (p.value == null ? '—' : String(p.value)),
      },
      {
        field: 'name',
        headerName: 'Project',
        flex: 1,
        minWidth: 240,
        cellRenderer: ProjectNameCellRenderer,
      },
      {
        field: 'status.name',
        headerName: 'Status',
        width: 140,
        cellRenderer: LifecycleStatusTagCellRenderer,
      },
      ...(scoringEnabled
        ? [
            {
              headerName: 'Score',
              width: 120,
              sortable: true,
              filter: false,
              // Sort by the numeric score; unscored projects sort as null.
              valueGetter: (p) => p.data?.currentScore?.value ?? null,
              cellRenderer: ScoreCellRenderer,
            } as ColDef<ProjectListDto>,
          ]
        : []),
    ],
    [canManage, dragEnabled, openProjectDrawer, scoringEnabled],
  )

  return (
    <>
      <WaydGrid
        ref={gridRef}
        columnDefs={columnDefs}
        rowData={orderedProjects}
        getRowId={getRowId}
        loading={isLoading}
        loadData={refetch}
        emptyMessage="No projects to rank."
        rowSelection={rowSelection}
        suppressCellFocus={true}
        rowDragManaged={dragEnabled}
        rowDragMultiRow={true}
        onRowDragEnd={onRowDragEnd}
        onSortChanged={syncInteractionState}
        onFilterChanged={syncInteractionState}
      />
      {selectedProjectKey && (
        <ProjectDrawer
          projectKey={selectedProjectKey}
          drawerOpen={drawerOpen}
          onDrawerClose={() => {
            setDrawerOpen(false)
            setSelectedProjectKey(null)
          }}
        />
      )}
    </>
  )
}

export default ProjectRankingBoard


'use client'

import { WaydGrid, WaydTooltip } from '@/src/components/common'
import {
  LifecycleStatusTagCellRenderer,
  ProgramLinkCellRenderer,
} from '@/src/components/common/wayd-grid-cell-renderers'
import { useMessage } from '@/src/components/contexts/messaging'
import ProjectDrawer from '@/src/app/ppm/_components/project-drawer'
import RecordProjectScoreForm from '@/src/app/ppm/projects/[key]/_components/scoring/record-project-score-form'
import {
  PortfolioRankingScoreboardDto,
  ProjectListDto,
} from '@/src/services/wayd-api'
import { useGetProjectScoringContextQuery } from '@/src/store/features/ppm/project-scores-api'
import { useMovePortfolioProjectRanksMutation } from '@/src/store/features/ppm/portfolios-api'
import { getSortedNames, isApiError } from '@/src/utils'
import {
  HolderOutlined,
  LoadingOutlined,
  MoreOutlined,
} from '@ant-design/icons'
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
import {
  KeyboardEvent,
  SyntheticEvent,
  useCallback,
  useMemo,
  useRef,
  useState,
} from 'react'
import styles from './project-ranking-board.module.css'
import { buildMoveRanksPayload, RankableRow } from './ranking'

export interface ProjectRankingBoardProps {
  portfolioId: string
  portfolioKey: number
  projects: ProjectListDto[]
  /** Current-model score breakdown per project + the model definition for criterion/output columns. */
  scoreboard?: PortfolioRankingScoreboardDto
  /** Whether the current user may rank (portfolio Update permission). */
  canManage: boolean
  isLoading?: boolean
  refetch: () => void
  refetchScoreboard: () => void
}

interface DragHandleCellProps extends ICellRendererParams<ProjectListDto> {
  /** Whether the user may rank projects at all. */
  canRank: boolean
  /** Whether dragging is currently allowed (false while sorted/filtered/searched). */
  dragEnabled: boolean
  menuItems: ItemType[]
}

interface ScoreCellRendererProps extends ICellRendererParams<ProjectListDto> {
  isOpening: boolean
  onOpenScoreForm: (projectId: string) => void
}

const stopRowClick = (event: SyntheticEvent) => {
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

// Renders the primary output value (already gated to the current-model score via the column's
// valueGetter) as a score action when the user may manage the project.
const ScoreCellRenderer = ({
  data,
  isOpening,
  onOpenScoreForm,
  value,
}: ScoreCellRendererProps) => {
  const scoreValue = value as number | null | undefined
  const hasScore = scoreValue != null
  const label = hasScore ? scoreValue.toFixed(2) : 'Unscored'

  if (!data?.canManageProject) {
    return (
      <Tag color={hasScore ? 'blue' : 'default'} style={{ marginInlineEnd: 0 }}>
        {label}
      </Tag>
    )
  }

  const actionTitle = hasScore
    ? 'Click to re-score project'
    : 'Click to score project'
  const openScore = () => onOpenScoreForm(data.id)
  const onScoreKeyDown = (event: KeyboardEvent<HTMLSpanElement>) => {
    if (event.key !== 'Enter' && event.key !== ' ') return
    event.preventDefault()
    stopRowClick(event)
    openScore()
  }

  return (
    <WaydTooltip title={actionTitle}>
      <Tag
        color={hasScore ? 'blue' : 'default'}
        className={styles.scoreTagAction}
        role="button"
        tabIndex={0}
        aria-busy={isOpening}
        onClick={(event) => {
          stopRowClick(event)
          openScore()
        }}
        onKeyDown={onScoreKeyDown}
        onMouseDown={stopRowClick}
      >
        {isOpening && <LoadingOutlined className={styles.scoreTagIcon} />}
        {label}
      </Tag>
    </WaydTooltip>
  )
}

const buildScoringColumnTooltip = (name: string, description?: string | null) =>
  [name, description?.trim()].filter(Boolean).join(' - ')

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
  scoreboard,
  canManage,
  isLoading,
  refetch,
  refetchScoreboard,
}: ProjectRankingBoardProps) => {
  const messageApi = useMessage()
  const gridRef = useRef<AgGridReact<ProjectListDto>>(null)
  const [moveRanks] = useMovePortfolioProjectRanksMutation()
  const [selectedProjectKey, setSelectedProjectKey] = useState<string | null>(
    null,
  )
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [scoreProjectId, setScoreProjectId] = useState<string | null>(null)

  const { currentData: scoringContext, isFetching: isScoringContextLoading } =
    useGetProjectScoringContextQuery(
      { projectId: scoreProjectId ?? '' },
      { skip: !scoreProjectId },
    )

  // Per-project breakdown lookups keyed by project id: criterion values by criterionId, output values
  // by token. Only projects whose current score matches the portfolio's current model are present, so
  // a missing entry (different/older model, or unscored) yields blank breakdown cells.
  const criterionValuesByProject = useMemo(() => {
    const map = new Map<string, Map<string, number>>()
    for (const p of scoreboard?.projects ?? []) {
      map.set(
        p.projectId,
        new Map(p.ratings.map((r) => [r.criterionId, r.ratingValue])),
      )
    }
    return map
  }, [scoreboard])

  const outputValuesByProject = useMemo(() => {
    const map = new Map<string, Map<string, number>>()
    for (const p of scoreboard?.projects ?? []) {
      map.set(p.projectId, new Map(p.outputs.map((o) => [o.token, o.value])))
    }
    return map
  }, [scoreboard])

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

  const openScoreForm = useCallback((projectId: string) => {
    setScoreProjectId(projectId)
  }, [])

  const closeScoreForm = () => {
    setScoreProjectId(null)
  }

  const onScoreFormComplete = () => {
    closeScoreForm()
    refetch()
    refetchScoreboard()
  }

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
        minWidth: 240,
        cellRenderer: ProjectNameCellRenderer,
      },
      {
        field: 'program.name',
        headerName: 'Program',
        width: 200,
        cellRenderer: (params: ICellRendererParams<ProjectListDto>) =>
          params.data?.program
            ? ProgramLinkCellRenderer({
                ...(params as any),
                data: params.data.program,
              })
            : null,
      },
      {
        field: 'status.name',
        headerName: 'Status',
        width: 140,
        cellRenderer: LifecycleStatusTagCellRenderer,
      },
      // Model-derived columns: criteria (in model order), then outputs (in model order). Headers use
      // the token. Values come from the project's current-model score; blank when there's no matching
      // score. The primary output renders as a badge to set it apart from the plain criterion/output
      // value cells.
      ...(scoreboard?.scoringModel?.criteria ?? []).map(
        (criterion) =>
          ({
            colId: `criterion:${criterion.id}`,
            headerName: criterion.token,
            headerTooltip: buildScoringColumnTooltip(
              criterion.name,
              criterion.description,
            ),
            width: 120,
            sortable: true,
            filter: false,
            valueGetter: (p) =>
              p.data
                ? (criterionValuesByProject.get(p.data.id)?.get(criterion.id) ??
                  null)
                : null,
            valueFormatter: (p) =>
              p.value == null ? '' : Number(p.value).toLocaleString(),
          }) as ColDef<ProjectListDto>,
      ),
      ...(scoreboard?.scoringModel?.outputs ?? []).map(
        (output) =>
          ({
            colId: `output:${output.token}`,
            headerName: output.token,
            headerTooltip: buildScoringColumnTooltip(
              output.name,
              `Formula: ${output.formula}`,
            ),
            width: 120,
            sortable: true,
            filter: false,
            valueGetter: (p) =>
              p.data
                ? (outputValuesByProject.get(p.data.id)?.get(output.token) ??
                  null)
                : null,
            valueFormatter: output.isPrimary
              ? undefined
              : (p) =>
                  p.value == null ? '' : Number(p.value).toLocaleString(),
            cellRenderer: output.isPrimary
              ? (params: ICellRendererParams<ProjectListDto>) => (
                  <ScoreCellRenderer
                    {...params}
                    isOpening={
                      isScoringContextLoading &&
                      params.data?.id === scoreProjectId
                    }
                    onOpenScoreForm={openScoreForm}
                  />
                )
              : undefined,
          }) as ColDef<ProjectListDto>,
      ),
      {
        field: 'start',
        width: 125,
        type: 'dateOnly',
      },
      {
        field: 'end',
        width: 125,
        type: 'dateOnly',
      },
      {
        field: 'projectManagers',
        headerName: 'PMs',
        valueGetter: (params) =>
          getSortedNames(params.data?.projectManagers ?? []),
      },
      {
        field: 'projectOwners',
        headerName: 'Owners',
        valueGetter: (params) =>
          getSortedNames(params.data?.projectOwners ?? []),
      },
    ],
    [
      canManage,
      dragEnabled,
      openProjectDrawer,
      openScoreForm,
      isScoringContextLoading,
      scoreProjectId,
      scoreboard?.scoringModel?.criteria,
      scoreboard?.scoringModel?.outputs,
      criterionValuesByProject,
      outputValuesByProject,
    ],
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
        onFirstDataRendered={syncInteractionState}
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
      {scoreProjectId && scoringContext?.scoringModel && (
        <RecordProjectScoreForm
          key={scoreProjectId}
          projectId={scoreProjectId}
          scoringModel={scoringContext.scoringModel}
          modelArchived={scoringContext.scoringModelArchived}
          currentScore={scoringContext.currentScore}
          onFormComplete={onScoreFormComplete}
          onFormCancel={closeScoreForm}
        />
      )}
    </>
  )
}

export default ProjectRankingBoard


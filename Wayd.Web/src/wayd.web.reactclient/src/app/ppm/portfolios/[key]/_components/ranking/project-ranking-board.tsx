'use client'

import { WaydTooltip } from '@/src/components/common'
import LifecycleStatusTag from '@/src/components/common/lifecycle-status-tag'
import {
  WaydGrid,
  renderProgramLink,
  renderProjectLink,
  useGridDragHandle,
  type GridColumnContext,
  type RowReorderEvent,
} from '@/src/components/common/wayd-grid'
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
import type { ColumnDef } from '@tanstack/react-table'
import { Button, Dropdown, Flex, Tag, theme } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import Link from 'next/link'
import { FC, KeyboardEvent, useCallback, useMemo, useState } from 'react'
import styles from './project-ranking-board.module.css'
import { buildMoveRanksPayload, RankableRow } from './ranking'

export interface ProjectRankingBoardProps {
  portfolioId: string
  portfolioKey: number
  projects: ProjectListDto[]
  /** Current-model score breakdown per project + the model definition for criterion/output columns. */
  scoreboard?: PortfolioRankingScoreboardDto
  /** Whether the current user may rank (portfolio Update permission AND is a portfolio Owner or Manager). */
  canManage: boolean
  isLoading?: boolean
  refetch: () => void
  refetchScoreboard: () => void
}

// Disabled (with tooltip) while sort/filter/search make the order ambiguous.
const DragHandleCell: FC<{ isDragEnabled: boolean }> = ({ isDragEnabled }) => {
  const { token } = theme.useToken()
  const { listeners, attributes } = useGridDragHandle()

  return (
    <WaydTooltip
      title={
        isDragEnabled
          ? undefined
          : 'Clear sorting, filters, and search to enable ranking.'
      }
    >
      <span
        {...(isDragEnabled ? { ...listeners, ...attributes } : {})}
        style={{
          cursor: isDragEnabled ? 'grab' : 'not-allowed',
          color: isDragEnabled
            ? token.colorTextTertiary
            : token.colorTextDisabled,
          display: 'inline-flex',
          padding: '0 4px',
          touchAction: 'none',
        }}
        aria-label="Drag to reorder"
        aria-disabled={!isDragEnabled}
      >
        <HolderOutlined />
      </span>
    </WaydTooltip>
  )
}

// Renders the primary output value (already gated to the current-model score via the column's
// accessor) as a score action when the user may manage the project.
const ScoreCell: FC<{
  project: ProjectListDto
  value: number | null | undefined
  isOpening: boolean
  onOpenScoreForm: (projectId: string) => void
}> = ({ project, value, isOpening, onOpenScoreForm }) => {
  const hasScore = value != null
  const label = hasScore ? value.toFixed(2) : 'Unscored'

  if (!project.canManageProject) {
    return (
      <Tag color={hasScore ? 'blue' : 'default'} style={{ marginInlineEnd: 0 }}>
        {label}
      </Tag>
    )
  }

  const actionTitle = hasScore
    ? 'Click to re-score project'
    : 'Click to score project'
  const openScore = () => onOpenScoreForm(project.id)
  const onScoreKeyDown = (event: KeyboardEvent<HTMLSpanElement>) => {
    if (event.key !== 'Enter' && event.key !== ' ') return
    event.preventDefault()
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
        onClick={openScore}
        onKeyDown={onScoreKeyDown}
      >
        {isOpening && <LoadingOutlined className={styles.scoreTagIcon} />}
        {label}
      </Tag>
    </WaydTooltip>
  )
}

const buildScoringColumnTooltip = (name: string, description?: string | null) =>
  [name, description?.trim()].filter(Boolean).join(' - ')

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

  // Translate the post-drop visual order into the move payload (moved id + its
  // ranked neighbours) — the server computes the new fractional ranks.
  const onRowReorder = async (event: RowReorderEvent<ProjectListDto>) => {
    const ordered: RankableRow[] = event.orderedData.map((p) => ({
      id: p.id,
      rank: p.rank,
    }))

    const payload = buildMoveRanksPayload(ordered, [event.toIndex])
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

  const columns = useMemo(
    () =>
      (context: GridColumnContext): ColumnDef<ProjectListDto, any>[] => [
        {
          id: 'actions',
          header: '',
          size: canManage ? 60 : 40,
          enableSorting: false,
          enableColumnFilter: false,
          enableResizing: false,
          enableGlobalFilter: false,
          cell: ({ row }) => {
            const menuItems: ItemType[] = [
              {
                key: 'view-details',
                label: 'View Details',
                onClick: () => openProjectDrawer(row.original.key),
              },
              {
                key: 'open-project',
                label: (
                  <Link href={`/ppm/projects/${row.original.key}`}>
                    Open Project
                  </Link>
                ),
              },
            ]

            return (
              <Flex align="center" gap={2}>
                {canManage && (
                  <DragHandleCell isDragEnabled={context.isDragEnabled} />
                )}
                <Dropdown menu={{ items: menuItems }} trigger={['click']}>
                  <Button type="text" size="small" icon={<MoreOutlined />} />
                </Dropdown>
              </Flex>
            )
          },
        },
        {
          id: 'position',
          accessorKey: 'position',
          header: 'Rank',
          size: 90,
          cell: ({ getValue }) => {
            const value = getValue<number | null>()
            return value == null ? '—' : String(value)
          },
        },
        {
          id: 'name',
          accessorKey: 'name',
          header: 'Project',
          minSize: 240,
          size: 240,
          cell: ({ row }) => renderProjectLink(row.original),
        },
        {
          id: 'program',
          accessorKey: 'program.name',
          header: 'Program',
          size: 200,
          meta: { filterEnableSet: true },
          cell: ({ row }) => renderProgramLink(row.original.program),
        },
        {
          id: 'status',
          accessorKey: 'status.name',
          header: 'Status',
          size: 140,
          meta: { filterType: 'set' },
          cell: ({ row }) =>
            row.original.status ? (
              <LifecycleStatusTag status={row.original.status} />
            ) : null,
        },
        // Model-derived columns: criteria (in model order), then outputs (in model order). Headers use
        // the token. Values come from the project's current-model score; blank when there's no matching
        // score. The primary output renders as a badge to set it apart from the plain criterion/output
        // value cells.
        ...(scoreboard?.scoringModel?.criteria ?? []).map(
          (criterion): ColumnDef<ProjectListDto, any> => ({
            id: `criterion:${criterion.id}`,
            accessorFn: (p) =>
              criterionValuesByProject.get(p.id)?.get(criterion.id) ?? null,
            header: criterion.token,
            size: 120,
            enableColumnFilter: false,
            meta: {
              headerTooltip: buildScoringColumnTooltip(
                criterion.name,
                criterion.description,
              ),
            },
            cell: ({ getValue }) => {
              const value = getValue<number | null>()
              return value == null ? '' : Number(value).toLocaleString()
            },
          }),
        ),
        ...(scoreboard?.scoringModel?.outputs ?? []).map(
          (output): ColumnDef<ProjectListDto, any> => ({
            id: `output:${output.token}`,
            accessorFn: (p) =>
              outputValuesByProject.get(p.id)?.get(output.token) ?? null,
            header: output.token,
            size: 120,
            enableColumnFilter: false,
            meta: {
              headerTooltip: buildScoringColumnTooltip(
                output.name,
                `Formula: ${output.formula}`,
              ),
            },
            cell: output.isPrimary
              ? ({ row, getValue }) => (
                  <ScoreCell
                    project={row.original}
                    value={getValue<number | null>()}
                    isOpening={
                      isScoringContextLoading &&
                      row.original.id === scoreProjectId
                    }
                    onOpenScoreForm={openScoreForm}
                  />
                )
              : ({ getValue }) => {
                  const value = getValue<number | null>()
                  return value == null ? '' : Number(value).toLocaleString()
                },
          }),
        ),
        {
          id: 'start',
          accessorKey: 'start',
          header: 'Start',
          size: 125,
          meta: { columnType: 'dateOnly' },
        },
        {
          id: 'end',
          accessorKey: 'end',
          header: 'End',
          size: 125,
          meta: { columnType: 'dateOnly' },
        },
        {
          id: 'projectManagers',
          accessorFn: (row) => getSortedNames(row.projectManagers ?? []),
          header: 'PMs',
        },
        {
          id: 'projectOwners',
          accessorFn: (row) => getSortedNames(row.projectOwners ?? []),
          header: 'Owners',
        },
      ],
    [
      canManage,
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
        columns={columns}
        data={orderedProjects}
        getRowId={(project) => project.id}
        isLoading={isLoading}
        onRefresh={refetch}
        emptyMessage="No projects to rank."
        persistStateKey="portfolio-project-ranking"
        csvFileName="portfolio-ranking"
        onRowReorder={canManage ? onRowReorder : undefined}
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

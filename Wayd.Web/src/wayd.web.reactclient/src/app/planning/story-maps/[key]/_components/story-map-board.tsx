'use client'

import { StoryMapDetailsDto } from '@/src/services/wayd-api'
import { useRemainingHeight } from '@/src/hooks'
import { PlusOutlined } from '@ant-design/icons'
import { Button } from 'antd'
import {
  CollisionDetection,
  DndContext,
  DragEndEvent,
  DragMoveEvent,
  DragOverlay,
  DragStartEvent,
  PointerSensor,
  pointerWithin,
  rectIntersection,
  useSensor,
  useSensors,
} from '@dnd-kit/core'
import { SortableContext } from '@dnd-kit/sortable'
import { CSSProperties, FC, Fragment, useMemo, useRef, useState } from 'react'
import { BoardActions } from './board-actions'
import { countTasksByLane } from './board-counts'
import {
  buildDragIndex,
  isValidDropTarget,
  parseEmptyStepSlotId,
  resolveDrop,
  taskCellId,
  type DropSide,
} from './board-drag'
import {
  buildBoardLayout,
  cellKey,
  GOAL_ROW,
  LABEL_COLUMN,
  STEP_ROW,
} from './board-layout'
import EmptyStepSlot from './empty-step-slot'
import GoalHeaderCell from './goal-header-cell'
import StepHeaderCell from './step-header-cell'
import SwimLaneHeader from './swim-lane-header'
import TaskCell from './task-cell'
import { useGoalRowHeight } from './use-goal-row-height'
import styles from '../../_components/story-map.module.css'

export interface StoryMapBoardProps {
  map: StoryMapDetailsDto
  selectedPersonaId: string | null
  actions: BoardActions
  onAddStep: (goalId: string) => void
  onAddSwimLane: () => void
  /** Lane and goal ids the viewer has folded away — local view state, not map data. */
  collapsedSwimLaneIds: ReadonlySet<string>
  onToggleSwimLaneCollapsed: (swimLaneId: string) => void
  collapsedGoalIds: ReadonlySet<string>
  onToggleGoalCollapsed: (goalId: string) => void
}

interface BoardGridCssVars extends CSSProperties {
  '--sm-step-columns': string
  /** Counts of each track kind, so the CSS can floor the board's width at their minimums. */
  '--sm-flexible-col-count': number
  '--sm-collapsed-col-count': number
  /** How far down the steps row pins, measured rather than assumed. */
  '--sm-goal-row-height': string
}

/**
 * The story map board: a single CSS grid so goals, steps, and tasks line up across the whole map
 * rather than nesting inside one another.
 *
 * Columns are `[label] [step] [step] …` — one track per step across every goal. Rows are
 * `[goals] [steps] [lane] [lane] …`. A goal header spans its own steps' tracks; each task cell is a
 * step column crossed with a swim-lane row. The label column is sticky so the row headers stay
 * readable while the board scrolls horizontally.
 */
const StoryMapBoard: FC<StoryMapBoardProps> = ({
  map,
  selectedPersonaId,
  actions,
  onAddStep,
  onAddSwimLane,
  collapsedSwimLaneIds,
  onToggleSwimLaneCollapsed,
  collapsedGoalIds,
  onToggleGoalCollapsed,
}) => {
  const layout = useMemo(
    () =>
      buildBoardLayout(map, {
        goalIds: collapsedGoalIds,
        swimLaneIds: collapsedSwimLaneIds,
      }),
    [map, collapsedGoalIds, collapsedSwimLaneIds],
  )
  const {
    goals,
    steps,
    swimLanes,
    tasksByCell,
    lastColumn,
    stepColumnTracks,
    flexibleColumnCount,
    collapsedColumnCount,
  } = layout

  const [goalRowRef, goalRowHeight] = useGoalRowHeight()

  // Sticky rows need a scrollport of their own to pin against, so the board takes the viewport
  // height that is left below it rather than letting the page scroll.
  const [scrollRef, scrollHeight] = useRemainingHeight()

  // An explicit track list rather than one repeat(): a collapsed goal's spine is a fixed width
  // while every other step column shares the leftover space equally.
  const gridStyle: BoardGridCssVars = {
    '--sm-step-columns': stepColumnTracks.join(' '),
    '--sm-flexible-col-count': flexibleColumnCount,
    '--sm-collapsed-col-count': collapsedColumnCount,
    '--sm-goal-row-height': `${goalRowHeight}px`,
  }

  // Everything placed in a task row iterates this rather than every lane. The predicate is a type
  // guard so `row` narrows to a number for the inline gridRow.
  const expandedSwimLanes = swimLanes.filter(
    (placement): placement is typeof placement & { row: number } =>
      placement.row !== null,
  )

  // The grid line just past the last lane row, which a collapsed goal's spine spans down to.
  const bottomRow =
    STEP_ROW +
    1 +
    swimLanes.reduce((rows, lane) => rows + (lane.isCollapsed ? 1 : 2), 0)

  // Deleting a lane moves its tasks to the default lane, so the confirmation says how many — every
  // task moves, not just the ones the filter leaves visible.
  const taskCountsByLane = useMemo(() => countTasksByLane(map, null), [map])

  // The banner count follows the filter instead, so it matches the cards on show in the lane's row.
  const visibleCountsByLane = useMemo(
    () => countTasksByLane(map, selectedPersonaId),
    [map, selectedPersonaId],
  )

  // A small activation distance so a click still reaches the cell's own controls.
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 4 } }),
  )

  const dragIndex = useMemo(() => buildDragIndex(layout), [layout])

  // Which side of the hovered node a drop lands on. Written to the ref during collision detection —
  // the only place with both the pointer position and the target's rect — then mirrored into state
  // from onDragMove. Setting state directly there would update this component while dnd-kit is
  // rendering, which React rejects.
  const dropSideRef = useRef<DropSide>('before')
  const [dropSide, setDropSide] = useState<DropSide>('before')

  // Restrict hit testing to legal targets, so an illegal one never highlights.
  const collisionDetection: CollisionDetection = useMemo(
    () => (args) => {
      const activeId = String(args.active.id)
      const droppableContainers = args.droppableContainers.filter((container) =>
        isValidDropTarget(dragIndex, activeId, String(container.id)),
      )

      // Pointer-based first so a large empty cell is reachable, falling back to rectangle
      // intersection when the pointer sits between targets.
      const collisions = pointerWithin({ ...args, droppableContainers })
      const resolved =
        collisions.length > 0
          ? collisions
          : rectIntersection({ ...args, droppableContainers })

      const top = resolved[0]
      const pointer = args.pointerCoordinates
      if (top && pointer) {
        const rect = args.droppableRects.get(top.id)
        if (rect) {
          // Tasks stack vertically; goals and steps read left to right.
          dropSideRef.current =
            dragIndex.kindById.get(activeId) === 'task'
              ? pointer.y > rect.top + rect.height / 2
                ? 'after'
                : 'before'
              : pointer.x > rect.left + rect.width / 2
                ? 'after'
                : 'before'
        }
      }

      return resolved
    },
    [dragIndex],
  )

  // onDragMove, not onDragOver: the side flips as the pointer crosses a target's midpoint, which is
  // not a change of target. Both setStates are no-ops unless the value actually changed.
  const handleDragMove = (event: DragMoveEvent) => {
    setDropSide((current) =>
      current === dropSideRef.current ? current : dropSideRef.current,
    )

    const next = event.over ? String(event.over.id) : null
    setOverId((current) => (current === next ? current : next))
  }

  // The node being dragged, rendered into the DragOverlay. Board nodes are grid children, so
  // transforming the original in place would slide it across its neighbours instead.
  const [activeDragId, setActiveDragId] = useState<string | null>(null)

  const [overId, setOverId] = useState<string | null>(null)

  const handleDragStart = (event: DragStartEvent) => {
    // Start neutral rather than inheriting the previous drag's side.
    dropSideRef.current = 'before'
    setDropSide('before')
    setOverId(null)
    setActiveDragId(String(event.active.id))
  }

  const handleDragEnd = (event: DragEndEvent) => {
    setActiveDragId(null)
    setOverId(null)

    const { active, over } = event
    if (!over) return

    const drop = resolveDrop(
      layout,
      dragIndex,
      String(active.id),
      String(over.id),
      dropSideRef.current,
    )
    if (drop) actions.onDrop(drop)
  }

  /**
   * The goal a dragged step would join, or null when the drag stays inside its current goal — a
   * reorder needs no destination marker, since the insertion line already says where it lands.
   */
  const receivingGoal = useMemo(() => {
    if (!activeDragId || !overId) return null
    if (dragIndex.kindById.get(activeDragId) !== 'step') return null

    const fromGoalId = dragIndex.goalIdByStepId.get(activeDragId)

    // Either hovering a sibling step, or the empty slot of a goal with no steps of its own.
    const toGoalId =
      dragIndex.goalIdByStepId.get(overId) ?? parseEmptyStepSlotId(overId)

    if (!toGoalId || toGoalId === fromGoalId) return null

    return goals.find((g) => g.goal.id === toGoalId) ?? null
  }, [activeDragId, overId, dragIndex, goals])

  /**
   * The cell a dragged task would land in, or null when that is the cell it already sits in. A
   * task's parent is the step crossed with the swim lane, so either axis changing is a reparent.
   */
  const receivingCellId = useMemo(() => {
    if (!activeDragId || !overId) return null
    if (dragIndex.kindById.get(activeDragId) !== 'task') return null

    const from = dragIndex.cellByTaskId.get(activeDragId)
    if (!from) return null

    // The target is either an empty cell, or a card — in which case the cell is the one it sits in.
    const overCell = dragIndex.cellByTaskId.get(overId)
    const to = overCell
      ? taskCellId(overCell.stepId, overCell.swimLaneId)
      : dragIndex.renderedCellIds.has(overId)
        ? overId
        : null

    return to && to !== taskCellId(from.stepId, from.swimLaneId) ? to : null
  }, [activeDragId, overId, dragIndex])

  // What to show inside the overlay: the name of whatever kind is being dragged.
  const activeDragLabel = useMemo(() => {
    if (!activeDragId) return null

    switch (dragIndex.kindById.get(activeDragId)) {
      case 'goal':
        return goals.find((g) => g.goal.id === activeDragId)?.goal.name ?? null
      case 'step':
        return steps.find((s) => s.step.id === activeDragId)?.step.name ?? null
      case 'swimLane':
        return (
          swimLanes.find((l) => l.swimLane.id === activeDragId)?.swimLane
            .name ?? null
        )
      case 'task':
        return (
          steps.flatMap((s) => s.step.tasks).find((t) => t.id === activeDragId)
            ?.title ?? null
        )
      default:
        return null
    }
  }, [activeDragId, dragIndex, goals, steps, swimLanes])

  // Every draggable id in one context: resolveDrop rejects mismatched kinds, so a single context
  // handles cross-parent moves without per-container collision juggling.
  const sortableIds = [
    ...goals.map((g) => g.goal.id),
    ...steps.map((s) => s.step.id),
    ...swimLanes.map((l) => l.swimLane.id),
  ]

  return (
    <DndContext
      sensors={sensors}
      collisionDetection={collisionDetection}
      onDragStart={handleDragStart}
      onDragMove={handleDragMove}
      onDragEnd={handleDragEnd}
      onDragCancel={() => {
        setActiveDragId(null)
        setOverId(null)
      }}
    >
      <div
        ref={scrollRef}
        className={styles.boardScroll}
        style={{ height: scrollHeight }}
      >
        <div className={styles.boardGrid} style={gridStyle} data-tour="board">
          <SortableContext items={sortableIds}>
            {/* ── Sticky label column ── */}
            <div
              ref={goalRowRef}
              className={`${styles.labelCell} ${styles.labelCellGoals}`}
              style={{ gridRow: GOAL_ROW, gridColumn: LABEL_COLUMN }}
            >
              Goals
            </div>
            <div
              className={`${styles.labelCell} ${styles.labelCellSteps}`}
              style={{ gridRow: STEP_ROW, gridColumn: LABEL_COLUMN }}
            >
              Steps
            </div>

            {/* ── Goals row ── */}
            {goals.map((placement) => (
              <GoalHeaderCell
                key={placement.goal.id}
                placement={placement}
                selectedPersonaId={selectedPersonaId}
                actions={actions}
                onAddStep={onAddStep}
                isLastColumn={
                  placement.columnStart + placement.columnSpan - 1 ===
                  lastColumn
                }
                dropSide={dropSide}
                onToggleCollapsed={onToggleGoalCollapsed}
                bottomRow={bottomRow}
              />
            ))}

            {/* ── Steps row ── */}
            {steps.map((placement) => (
              <StepHeaderCell
                key={placement.step.id}
                placement={placement}
                selectedPersonaId={selectedPersonaId}
                actions={actions}
                isLastColumn={placement.column === lastColumn}
                dropSide={dropSide}
              />
            ))}

            {/* Its own overlay rather than a class on the step cells: outlining each cell would
                draw interior edges between them instead of one boundary round the run. */}
            {receivingGoal && (
              <div
                className={styles.receivingStepBand}
                style={{
                  gridRow: STEP_ROW,
                  gridColumn: `${receivingGoal.columnStart} / span ${receivingGoal.columnSpan}`,
                }}
              />
            )}

            {/* ── Task band: per swim lane, a full-width header banner then a row of task cells ── */}
            {swimLanes.map(({ swimLane, headerRow, isCollapsed }) => (
              <SwimLaneHeader
                key={`lane-header-${swimLane.id}`}
                swimLane={swimLane}
                row={headerRow}
                taskCount={taskCountsByLane.get(swimLane.id) ?? 0}
                visibleTaskCount={visibleCountsByLane.get(swimLane.id) ?? 0}
                isCollapsed={isCollapsed}
                onToggleCollapsed={onToggleSwimLaneCollapsed}
                actions={actions}
              />
            ))}

            {/* Empty filler beside each lane's task row, so the label column's right border continues
            past the Steps row. The lane name itself lives in the banner above. */}
            {expandedSwimLanes.map(({ swimLane, row }) => (
              <div
                key={`lane-spacer-${swimLane.id}`}
                className={`${styles.labelCell} ${styles.labelCellSpacer}`}
                style={{ gridRow: row, gridColumn: LABEL_COLUMN }}
              />
            ))}

            {expandedSwimLanes.map(({ swimLane, row }) =>
              steps.map(({ step, column }) => (
                <TaskCell
                  key={cellKey(step.id, swimLane.id)}
                  cellId={taskCellId(step.id, swimLane.id)}
                  tasks={tasksByCell.get(cellKey(step.id, swimLane.id)) ?? []}
                  column={column}
                  row={row}
                  selectedPersonaId={selectedPersonaId}
                  actions={actions}
                  isLastColumn={column === lastColumn}
                  dropSide={dropSide}
                  isReceiving={
                    taskCellId(step.id, swimLane.id) === receivingCellId
                  }
                />
              )),
            )}

            {/* ── Blank cells filling a step-less goal's placeholder track ── */}
            {goals
              .filter((placement) => placement.isPlaceholderColumn)
              .map((placement) => (
                <Fragment key={`placeholder-${placement.goal.id}`}>
                  <EmptyStepSlot
                    goalId={placement.goal.id}
                    column={placement.columnStart}
                    canUpdate={actions.canUpdate}
                    isLastColumn={placement.columnStart === lastColumn}
                    onAddStep={onAddStep}
                  />
                  {expandedSwimLanes.map(({ swimLane, row }) => (
                    <div
                      key={`placeholder-${placement.goal.id}-${swimLane.id}`}
                      className={`${styles.taskCell} ${
                        placement.columnStart === lastColumn
                          ? styles.lastColumn
                          : ''
                      }`}
                      style={{
                        gridRow: row,
                        gridColumn: placement.columnStart,
                      }}
                    />
                  ))}
                </Fragment>
              ))}

            {/* ── Add swim lane: a full-width footer under the last lane row. Always rendered (empty
            when the user cannot edit) so it forms the grid's bottom edge and the lane cells above
            keep their own bottom border. ── */}
            <div
              className={styles.addSwimLaneCell}
              style={{ gridColumn: '1 / -1' }}
            >
              {actions.canUpdate && (
                <Button
                  type="text"
                  size="small"
                  icon={<PlusOutlined />}
                  data-tour="add-swim-lane"
                  onClick={onAddSwimLane}
                >
                  Add swim lane
                </Button>
              )}
            </div>
          </SortableContext>
        </div>
      </div>

      {/* The floating copy that follows the cursor. A plain labelled chip rather than a clone of the
          card: the real cells are sized by their grid track, which does not exist outside the grid,
          and their inline controls would be dead weight on something being carried. */}
      <DragOverlay dropAnimation={null}>
        {activeDragLabel && (
          <div className={styles.dragOverlayCard}>{activeDragLabel}</div>
        )}
      </DragOverlay>
    </DndContext>
  )
}

export default StoryMapBoard

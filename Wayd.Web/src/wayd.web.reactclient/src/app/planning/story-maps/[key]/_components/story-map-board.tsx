'use client'

import { StoryMapDetailsDto } from '@/src/services/wayd-api'
import { PlusOutlined } from '@ant-design/icons'
import { Button } from 'antd'
import {
  CollisionDetection,
  DndContext,
  DragEndEvent,
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
import {
  buildDragIndex,
  isValidDropTarget,
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
import styles from '../../_components/story-map.module.css'

export interface StoryMapBoardProps {
  map: StoryMapDetailsDto
  selectedPersonaId: string | null
  actions: BoardActions
  onAddStep: (goalId: string) => void
  onAddSwimLane: () => void
}

interface BoardGridCssVars extends CSSProperties {
  '--sm-step-columns': string
  '--sm-step-count': number
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
}) => {
  const layout = useMemo(() => buildBoardLayout(map), [map])
  const { goals, steps, swimLanes, tasksByCell, lastColumn, stepColumnCount } =
    layout

  // Every step column is the same width: at least --sm-col-min, sharing any leftover space equally
  // so the board fills the screen when it is narrow and scrolls when it is wide. The count is
  // published as a variable too, so the CSS can floor the board at the sum of the track minimums.
  const gridStyle: BoardGridCssVars = {
    '--sm-step-columns': `repeat(${stepColumnCount}, minmax(var(--sm-col-min), 1fr))`,
    '--sm-step-count': stepColumnCount,
  }

  // Deleting a lane moves its tasks to the default lane rather than deleting them, so the
  // confirmation says how many will move.
  const taskCountsByLane = useMemo(() => {
    const counts = new Map<string, number>()
    for (const goal of map.goals) {
      for (const step of goal.steps) {
        for (const task of step.tasks) {
          counts.set(task.swimLaneId, (counts.get(task.swimLaneId) ?? 0) + 1)
        }
      }
    }
    return counts
  }, [map])

  // A small activation distance so a click still reaches the inline editors, persona dots, and
  // hover actions rather than being swallowed as a drag — same as the persona reordering modal.
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 4 } }),
  )

  const dragIndex = useMemo(() => buildDragIndex(layout), [layout])

  // Which half of the hovered node the pointer is in, and hence which side of it a drop lands on.
  // Keeping the insertion point inside a node's own box (rather than on the border between two) is
  // what makes the seam between adjacent goals unambiguous.
  //
  // Written in the ref during collision detection — the only place with both the pointer position
  // and the target's measured rect — and mirrored into state from onDragOver. Calling setState from
  // collision detection instead would update this component while dnd-kit is rendering, which React
  // rejects ("Cannot update a component while rendering a different component").
  const dropSideRef = useRef<DropSide>('before')
  const [dropSide, setDropSide] = useState<DropSide>('before')

  // Restrict hit testing to targets the dragged node may legally land on, so an illegal one never
  // highlights or opens a gap — a goal dragged over a task simply finds nothing there.
  const collisionDetection: CollisionDetection = useMemo(
    () => (args) => {
      const activeId = String(args.active.id)
      const droppableContainers = args.droppableContainers.filter((container) =>
        isValidDropTarget(dragIndex, activeId, String(container.id)),
      )

      // Pointer-based first, so a large empty task cell is reachable; fall back to rectangle
      // intersection when the pointer is between targets (dragging past a cell's padding, say).
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

  // Publish the side the insertion line should render on. onDragMove (not onDragOver) because the
  // side changes as the pointer crosses a target's midpoint, which is not a change of target —
  // onDragOver would only fire on the way into a different node, leaving the line stuck on one edge.
  // The setState is a no-op unless the side actually flipped, so this does not re-render per pixel.
  const handleDragMove = () => {
    setDropSide((current) =>
      current === dropSideRef.current ? current : dropSideRef.current,
    )
  }

  // The node currently under the cursor, rendered into the DragOverlay. Without an overlay the
  // original node is transformed in place, which — because every node here is a grid child — slides
  // it across its neighbours and reads as broken styling rather than as a card being carried.
  const [activeDragId, setActiveDragId] = useState<string | null>(null)

  const handleDragStart = (event: DragStartEvent) => {
    // Start neutral rather than inheriting the previous drag's side.
    dropSideRef.current = 'before'
    setDropSide('before')
    setActiveDragId(String(event.active.id))
  }

  const handleDragEnd = (event: DragEndEvent) => {
    setActiveDragId(null)

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
          swimLanes.find((l) => l.swimLane.id === activeDragId)?.swimLane.name ??
          null
        )
      case 'task':
        return (
          steps
            .flatMap((s) => s.step.tasks)
            .find((t) => t.id === activeDragId)?.title ?? null
        )
      default:
        return null
    }
  }, [activeDragId, dragIndex, goals, steps, swimLanes])

  // Every draggable id in one context. The four kinds never interleave — resolveDrop rejects a drop
  // whose target is the wrong kind — so a single context keeps cross-parent moves working without
  // per-container collision juggling.
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
      onDragCancel={() => setActiveDragId(null)}
    >
    <div className={styles.boardScroll}>
      <div className={styles.boardGrid} style={gridStyle}>
      <SortableContext items={sortableIds}>
        {/* ── Sticky label column ── */}
        <div
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
              placement.columnStart + placement.columnSpan - 1 === lastColumn
            }
            dropSide={dropSide}
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

        {/* ── Task band: per swim lane, a full-width header banner then a row of task cells ── */}
        {swimLanes.map(({ swimLane, headerRow }) => (
          <SwimLaneHeader
            key={`lane-header-${swimLane.id}`}
            swimLane={swimLane}
            row={headerRow}
            taskCount={taskCountsByLane.get(swimLane.id) ?? 0}
            actions={actions}
          />
        ))}

        {/* An empty label-column cell beside each lane's task row. The lane name lives in the
            full-width banner above, but without a cell here the label column's right border — the
            line separating it from the first task column — would stop at the Steps row. */}
        {swimLanes.map(({ swimLane, row }) => (
          <div
            key={`lane-spacer-${swimLane.id}`}
            className={`${styles.labelCell} ${styles.labelCellSpacer}`}
            style={{ gridRow: row, gridColumn: LABEL_COLUMN }}
          />
        ))}

        {swimLanes.map(({ swimLane, row }) =>
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
            />
          )),
        )}

        {/* ── Blank cells filling the placeholder track of a step-less goal, so the steps row and
            task rows have no holes under it. ── */}
        {goals
          .filter((placement) => placement.isPlaceholderColumn)
          .map((placement) => (
            <Fragment key={`placeholder-${placement.goal.id}`}>
              <EmptyStepSlot
                goalId={placement.goal.id}
                column={placement.columnStart}
                canUpdate={actions.canUpdate}
                isLastColumn={placement.columnStart === lastColumn}
              />
              {swimLanes.map(({ swimLane, row }) => (
                <div
                  key={`placeholder-${placement.goal.id}-${swimLane.id}`}
                  className={`${styles.taskCell} ${
                    placement.columnStart === lastColumn ? styles.lastColumn : ''
                  }`}
                  style={{ gridRow: row, gridColumn: placement.columnStart }}
                />
              ))}
            </Fragment>
          ))}

        {/* ── Add swim lane: a full-width footer under the last lane row. Always rendered (empty
            when the user cannot edit) so it forms the grid's bottom edge and the lane cells above
            keep their own bottom border. ── */}
        <div className={styles.addSwimLaneCell} style={{ gridColumn: '1 / -1' }}>
          {actions.canUpdate && (
            <Button
              type="text"
              size="small"
              icon={<PlusOutlined />}
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

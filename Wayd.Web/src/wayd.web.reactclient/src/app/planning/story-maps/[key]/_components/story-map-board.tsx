'use client'

import { StoryMapDetailsDto } from '@/src/services/wayd-api'
import { PlusOutlined } from '@ant-design/icons'
import { Button } from 'antd'
import { CSSProperties, FC, Fragment, useMemo } from 'react'
import { BoardActions } from './board-actions'
import {
  buildBoardLayout,
  cellKey,
  GOAL_ROW,
  LABEL_COLUMN,
  STEP_ROW,
} from './board-layout'
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

  return (
    <div className={styles.boardScroll}>
      <div className={styles.boardGrid} style={gridStyle}>
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
              tasks={tasksByCell.get(cellKey(step.id, swimLane.id)) ?? []}
              column={column}
              row={row}
              selectedPersonaId={selectedPersonaId}
              actions={actions}
              isLastColumn={column === lastColumn}
            />
          )),
        )}

        {/* ── Blank cells filling the placeholder track of a step-less goal, so the steps row and
            task rows have no holes under it. ── */}
        {goals
          .filter((placement) => placement.isPlaceholderColumn)
          .map((placement) => (
            <Fragment key={`placeholder-${placement.goal.id}`}>
              <div
                className={`${styles.stepCell} ${
                  placement.columnStart === lastColumn ? styles.lastColumn : ''
                }`}
                style={{ gridRow: STEP_ROW, gridColumn: placement.columnStart }}
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
      </div>
    </div>
  )
}

export default StoryMapBoard

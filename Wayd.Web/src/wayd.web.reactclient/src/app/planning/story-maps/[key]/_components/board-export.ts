import { StoryMapDetailsDto } from '@/src/services/wayd-api'

/**
 * Flattening a story map to CSV.
 *
 * A task is the only node identified by the whole structure — its goal, step, and swim lane together
 * place it — so the file is one row per task with the ancestors repeated. Goals and steps holding
 * nothing still get a row, with the columns below them blank, so the export records the map's shape
 * rather than only its leaves.
 *
 * Rows come out in board order (goal, then step, then task) and the persona filter is ignored: an
 * export is the whole map, not the current view.
 */

export const EXPORT_HEADERS = [
  'Goal',
  'Goal Order',
  'Step',
  'Step Order',
  'Task',
  'Task Order',
  'Description',
  'Personas',
  'Swim Lane',
]

/** Comma is the delimiter, so a multi-value cell separates with semicolons. */
const MULTI_VALUE_SEPARATOR = '; '

const byOrder = <T extends { order: number }>(items: T[]): T[] =>
  [...items].sort((a, b) => a.order - b.order)

export const buildExportRows = (map: StoryMapDetailsDto): unknown[][] => {
  const personaNames = new Map(map.personas.map((p) => [p.id, p.name]))
  const laneNames = new Map(map.swimLanes.map((l) => [l.id, l.name]))

  // Personas are listed in the map's own order rather than the order they were linked, so the same
  // pair of personas always reads the same way down the column.
  const orderedPersonaIds = byOrder(map.personas).map((p) => p.id)
  const formatPersonas = (personaIds: string[]) =>
    orderedPersonaIds
      .filter((id) => personaIds.includes(id))
      .map((id) => personaNames.get(id) ?? '')
      .join(MULTI_VALUE_SEPARATOR)

  const rows: unknown[][] = []

  byOrder(map.goals).forEach((goal, goalIndex) => {
    const goalOrder = goalIndex + 1

    if (goal.steps.length === 0) {
      rows.push([goal.name, goalOrder, '', '', '', '', '', '', ''])
      return
    }

    byOrder(goal.steps).forEach((step, stepIndex) => {
      const stepOrder = stepIndex + 1

      if (step.tasks.length === 0) {
        rows.push([
          goal.name,
          goalOrder,
          step.name,
          stepOrder,
          '',
          '',
          '',
          formatPersonas(step.personaIds),
          '',
        ])
        return
      }

      // Task order is scoped to a (step × swim lane) cell, so the lanes are walked in board order
      // and each cell numbered from 1 — sorting the step's tasks as one list would interleave lanes.
      // A task naming a lane the map does not have would fall through that walk, so it trails the
      // known lanes rather than being dropped from the file.
      const laneIds = byOrder(map.swimLanes).map((l) => l.id)
      const orphanLaneIds = [
        ...new Set(
          step.tasks
            .map((t) => t.swimLaneId)
            .filter((id) => !laneNames.has(id)),
        ),
      ]

      ;[...laneIds, ...orphanLaneIds].forEach((laneId) => {
        const cellTasks = byOrder(
          step.tasks.filter((t) => t.swimLaneId === laneId),
        )

        cellTasks.forEach((task, taskIndex) => {
          rows.push([
            goal.name,
            goalOrder,
            step.name,
            stepOrder,
            task.title,
            taskIndex + 1,
            task.description ?? '',
            formatPersonas(task.personaIds),
            laneNames.get(task.swimLaneId) ?? '',
          ])
        })
      })
    })
  })

  return rows
}

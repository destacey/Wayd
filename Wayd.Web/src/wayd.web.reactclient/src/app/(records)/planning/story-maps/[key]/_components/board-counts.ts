import { StoryMapDetailsDto } from '@/src/services/wayd-api'

/**
 * Board totals for the persona filter bar.
 *
 * These mirror the muting rules on the board itself, so the numbers always agree with what is lit:
 * steps and tasks count on their own persona tags, while a goal counts whenever anything beneath it
 * does — a goal is a container, and nothing in the UI tags one directly.
 */
export interface BoardCounts {
  goals: number
  steps: number
  tasks: number
}

/**
 * Tasks per swim lane, keyed by lane id. Honours the persona filter so a lane's banner count agrees
 * with the cards left visible in its row; pass null to count every task.
 */
export const countTasksByLane = (
  map: StoryMapDetailsDto,
  selectedPersonaId: string | null,
): Map<string, number> => {
  const counts = new Map<string, number>()

  for (const goal of map.goals) {
    for (const step of goal.steps) {
      for (const task of step.tasks) {
        if (
          selectedPersonaId !== null &&
          !task.personaIds.includes(selectedPersonaId)
        ) {
          continue
        }

        counts.set(task.swimLaneId, (counts.get(task.swimLaneId) ?? 0) + 1)
      }
    }
  }

  return counts
}

export const countBoard = (
  map: StoryMapDetailsDto,
  selectedPersonaId: string | null,
): BoardCounts => {
  if (selectedPersonaId === null) {
    let steps = 0
    let tasks = 0
    for (const goal of map.goals) {
      steps += goal.steps.length
      for (const step of goal.steps) tasks += step.tasks.length
    }
    return { goals: map.goals.length, steps, tasks }
  }

  const counts: BoardCounts = { goals: 0, steps: 0, tasks: 0 }

  for (const goal of map.goals) {
    let goalMatches = goal.personaIds.includes(selectedPersonaId)

    for (const step of goal.steps) {
      if (step.personaIds.includes(selectedPersonaId)) {
        counts.steps += 1
        goalMatches = true
      }

      for (const task of step.tasks) {
        if (task.personaIds.includes(selectedPersonaId)) {
          counts.tasks += 1
          goalMatches = true
        }
      }
    }

    if (goalMatches) counts.goals += 1
  }

  return counts
}

import { StoryMapPersonaDto, StoryMapTaskDto } from '@/src/services/wayd-api'

/**
 * Shared board-editing callbacks, threaded down the goal → step → task tree so each card can
 * rename/delete itself and add children without the page passing individual handlers at every level.
 */
export interface BoardActions {
  canUpdate: boolean
  /** The id of an item just created inline, so its name field opens in edit mode. */
  autoEditId: string | null
  /** Every persona on the map, so step and task footers can offer one toggle dot each. */
  personas: StoryMapPersonaDto[]
  onRenameGoal: (goalId: string, name: string) => void
  onDeleteGoal: (goalId: string) => void
  onRenameStep: (stepId: string, name: string) => void
  onDeleteStep: (stepId: string) => void
  onAddTask: (stepId: string) => void
  onRenameTask: (task: StoryMapTaskDto, title: string) => void
  onDeleteTask: (taskId: string) => void
  /** Link or unlink a single persona; the handler sends the resulting full list to the API. */
  onToggleStepPersona: (stepId: string, personaId: string) => void
  onToggleTaskPersona: (taskId: string, personaId: string) => void
}

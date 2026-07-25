import { StoryMapTaskDto } from '@/src/services/wayd-api'

/**
 * Shared board-editing callbacks, threaded down the goal → step → task tree so each card can
 * rename/delete itself and add children without the page passing individual handlers at every level.
 */
export interface BoardActions {
  canUpdate: boolean
  /** The id of an item just created inline, so its name field opens in edit mode. */
  autoEditId: string | null
  onRenameGoal: (goalId: string, name: string) => void
  onDeleteGoal: (goalId: string) => void
  onRenameStep: (stepId: string, name: string) => void
  onDeleteStep: (stepId: string) => void
  onAddTask: (stepId: string) => void
  onRenameTask: (task: StoryMapTaskDto, title: string) => void
  onDeleteTask: (taskId: string) => void
}

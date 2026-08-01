import { StoryMapPersonaDto, StoryMapTaskDto } from '@/src/services/wayd-api'
import { DropResult } from './board-drag'

/**
 * Shared board-editing callbacks, threaded down the goal → step → task tree so each card can
 * rename/delete itself and add children without the page passing individual handlers at every level.
 */
export interface BoardActions {
  canUpdate: boolean
  /** The id of an item just created inline, so its name field opens in edit mode. */
  autoEditId: string | null
  /**
   * Clears {@link autoEditId} once the editor closes. Without this the flag outlives the edit, and
   * anything that remounts the node — dragging it to another cell — reopens the editor.
   */
  onAutoEditEnd: () => void
  /** Every persona on the map, so step and task footers can offer one toggle dot each. */
  personas: StoryMapPersonaDto[]
  onRenameGoal: (goalId: string, name: string) => void
  onDeleteGoal: (goalId: string) => void
  onRenameStep: (stepId: string, name: string) => void
  onDeleteStep: (stepId: string) => void
  /** Omitting the lane adds to the default one; a task cell passes its own. */
  onAddTask: (stepId: string, swimLaneId?: string) => void
  onRenameTask: (task: StoryMapTaskDto, title: string) => void
  onDeleteTask: (taskId: string) => void
  /** Link or unlink a single persona; the handler sends the resulting full list to the API. */
  onToggleStepPersona: (stepId: string, personaId: string) => void
  onToggleTaskPersona: (taskId: string, personaId: string) => void
  /** The default lane cannot be renamed, dated, or removed, so these are never called for it. */
  onRenameSwimLane: (swimLaneId: string, name: string) => void
  onDeleteSwimLane: (swimLaneId: string) => void
  /** Either date may be undefined — a lane can carry only a start, only an end, or neither. */
  onSetSwimLaneDates: (
    swimLaneId: string,
    startDate: Date | undefined,
    endDate: Date | undefined,
  ) => void
  /** A completed drag, already resolved to which node moved and where. */
  onDrop: (drop: DropResult) => void
}

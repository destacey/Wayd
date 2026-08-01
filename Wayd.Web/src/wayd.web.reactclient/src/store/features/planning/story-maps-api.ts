import { getStoryMapsClient } from '@/src/services/clients'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import {
  AddChecklistItemRequest,
  AddGoalRequest,
  AddSwimLaneRequest,
  AddPersonaRequest,
  AddStepRequest,
  AddTaskRequest,
  CreateStoryMapRequest,
  LinkWorkItemRequest,
  MoveStepRequest,
  MoveTaskRequest,
  ObjectIdAndKey,
  RenameChecklistItemRequest,
  RenameGoalRequest,
  RenameStepRequest,
  RenameSwimLaneRequest,
  RenameTaskRequest,
  SetChecklistItemCheckedRequest,
  SetTaskDescriptionRequest,
  SetSwimLaneDatesRequest,
  SetStepPersonasRequest,
  SetTaskPersonasRequest,
  StoryMapDetailsDto,
  StoryMapGoalDto,
  StoryMapSwimLaneDto,
  StoryMapListDto,
  StoryMapPersonaDto,
  StoryMapStepDto,
  StoryMapTaskDto,
  UpdatePersonaRequest,
  UpdateStoryMapRequest,
} from '@/src/services/wayd-api'
import {
  applyMoveStep,
  applyMoveTask,
  applyRemoveSwimLane,
  findTaskInDraft,
  recountChecklist,
  reorderInPlace,
  tempId,
} from './story-map-patches'

export const storyMapsApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getStoryMaps: builder.query<StoryMapListDto[], boolean | undefined>({
      queryFn: async (includeArchived) => {
        try {
          const data = await getStoryMapsClient().getList(includeArchived)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [{ type: QueryTags.StoryMapList, id: 'LIST' }],
    }),

    getStoryMap: builder.query<StoryMapDetailsDto, string>({
      queryFn: async (idOrKey: string) => {
        try {
          const data = await getStoryMapsClient().get(idOrKey)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result) => [{ type: QueryTags.StoryMap, id: result?.id }],
    }),

    createStoryMap: builder.mutation<ObjectIdAndKey, CreateStoryMapRequest>({
      queryFn: async (request) => {
        try {
          const data = await getStoryMapsClient().create(request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [{ type: QueryTags.StoryMapList, id: 'LIST' }],
    }),

    updateStoryMap: builder.mutation<
      null,
      { id: string; request: UpdateStoryMapRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          await getStoryMapsClient().update(id, request)
          // RTK Query requires a defined `data` value; a void endpoint returns null, not undefined.
          return { data: null }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (_r, _e, { id }) => [
        { type: QueryTags.StoryMap, id },
        { type: QueryTags.StoryMapList, id: 'LIST' },
      ],
    }),

    archiveStoryMap: builder.mutation<null, { id: string }>({
      queryFn: async ({ id }) => {
        try {
          await getStoryMapsClient().archive(id)
          return { data: null }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (_r, _e, { id }) => [
        { type: QueryTags.StoryMap, id },
        { type: QueryTags.StoryMapList, id: 'LIST' },
      ],
    }),

    deleteStoryMap: builder.mutation<null, { id: string }>({
      queryFn: async ({ id }) => {
        try {
          await getStoryMapsClient().delete(id)
          return { data: null }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      // Only the list. Invalidating the single-map tag would make the detail page — still mounted
      // and subscribed while it navigates away — refetch the map that was just deleted and 404.
      invalidatesTags: () => [{ type: QueryTags.StoryMapList, id: 'LIST' }],
    }),

    // ---- Structural mutations (invalidate the single map; SignalR keeps other
    // clients current, and the local client refetches the map graph). ----

    addGoal: builder.mutation<
      StoryMapGoalDto,
      { storyMapId: string; request: AddGoalRequest }
    >({
      queryFn: async ({ storyMapId, request }) => {
        try {
          const data = await getStoryMapsClient().addGoal(storyMapId, request)
          return { data }
        } catch (error) {
          return { error }
        }
      },
      invalidatesTags: (_r, _e, { storyMapId }) => [
        { type: QueryTags.StoryMap, id: storyMapId },
      ],
    }),

    addStep: builder.mutation<
      StoryMapStepDto,
      { storyMapId: string; request: AddStepRequest }
    >({
      queryFn: async ({ storyMapId, request }) => {
        try {
          const data = await getStoryMapsClient().addStep(storyMapId, request)
          return { data }
        } catch (error) {
          return { error }
        }
      },
      invalidatesTags: (_r, _e, { storyMapId }) => [
        { type: QueryTags.StoryMap, id: storyMapId },
      ],
    }),

    addTask: builder.mutation<
      StoryMapTaskDto,
      { storyMapId: string; request: AddTaskRequest }
    >({
      queryFn: async ({ storyMapId, request }) => {
        try {
          const data = await getStoryMapsClient().addTask(storyMapId, request)
          return { data }
        } catch (error) {
          return { error }
        }
      },
      invalidatesTags: (_r, _e, { storyMapId }) => [
        { type: QueryTags.StoryMap, id: storyMapId },
      ],
    }),

    renameGoal: builder.mutation<
      null,
      {
        storyMapId: string
        storyMapKey: string
        goalId: string
        request: RenameGoalRequest
      }
    >({
      queryFn: async ({ storyMapId, goalId, request }) => {
        try {
          await getStoryMapsClient().renameGoal(storyMapId, goalId, request)
          return { data: null }
        } catch (error) {
          return { error }
        }
      },
      // Patch the goal name in the cache up front so the rename shows instantly. Roll back on error.
      onQueryStarted: async (
        { storyMapKey, goalId, request },
        { dispatch, queryFulfilled },
      ) => {
        const patchResult = dispatch(
          storyMapsApi.util.updateQueryData(
            'getStoryMap',
            storyMapKey,
            (draft) => {
              const goal = draft.goals.find((g) => g.id === goalId)
              if (goal) goal.name = request.name
            },
          ),
        )
        try {
          await queryFulfilled
        } catch {
          patchResult.undo()
        }
      },
      invalidatesTags: (_r, _e, { storyMapId }) => [
        { type: QueryTags.StoryMap, id: storyMapId },
      ],
    }),

    deleteGoal: builder.mutation<
      null,
      { storyMapId: string; storyMapKey: string; goalId: string }
    >({
      queryFn: async ({ storyMapId, goalId }) => {
        try {
          await getStoryMapsClient().deleteGoal(storyMapId, goalId)
          return { data: null }
        } catch (error) {
          return { error }
        }
      },
      onQueryStarted: async (
        { storyMapKey, goalId },
        { dispatch, queryFulfilled },
      ) => {
        const patchResult = dispatch(
          storyMapsApi.util.updateQueryData(
            'getStoryMap',
            storyMapKey,
            (draft) => {
              draft.goals = draft.goals.filter((g) => g.id !== goalId)
            },
          ),
        )
        try {
          await queryFulfilled
        } catch {
          patchResult.undo()
        }
      },
      invalidatesTags: (_r, _e, { storyMapId }) => [
        { type: QueryTags.StoryMap, id: storyMapId },
      ],
    }),

    reorderGoal: builder.mutation<
      null,
      {
        storyMapId: string
        storyMapKey: string
        goalId: string
        newOrder: number
      }
    >({
      queryFn: async ({ storyMapId, goalId, newOrder }) => {
        try {
          await getStoryMapsClient().reorderGoal(storyMapId, goalId, {
            newOrder,
          })
          return { data: null }
        } catch (error) {
          return { error }
        }
      },
      // Move and renumber contiguously so the board re-lays-out instantly; roll back on failure.
      onQueryStarted: async (
        { storyMapKey, goalId, newOrder },
        { dispatch, queryFulfilled },
      ) => {
        const patchResult = dispatch(
          storyMapsApi.util.updateQueryData(
            'getStoryMap',
            storyMapKey,
            (draft) => {
              reorderInPlace(draft.goals, goalId, newOrder)
            },
          ),
        )
        try {
          await queryFulfilled
        } catch {
          patchResult.undo()
        }
      },
      invalidatesTags: (_r, _e, { storyMapId }) => [
        { type: QueryTags.StoryMap, id: storyMapId },
      ],
    }),

    reorderStep: builder.mutation<
      null,
      {
        storyMapId: string
        storyMapKey: string
        stepId: string
        newOrder: number
      }
    >({
      queryFn: async ({ storyMapId, stepId, newOrder }) => {
        try {
          await getStoryMapsClient().reorderStep(storyMapId, stepId, {
            newOrder,
          })
          return { data: null }
        } catch (error) {
          return { error }
        }
      },
      onQueryStarted: async (
        { storyMapKey, stepId, newOrder },
        { dispatch, queryFulfilled },
      ) => {
        const patchResult = dispatch(
          storyMapsApi.util.updateQueryData(
            'getStoryMap',
            storyMapKey,
            (draft) => {
              const goal = draft.goals.find((g) =>
                g.steps.some((s) => s.id === stepId),
              )
              if (goal) reorderInPlace(goal.steps, stepId, newOrder)
            },
          ),
        )
        try {
          await queryFulfilled
        } catch {
          patchResult.undo()
        }
      },
      invalidatesTags: (_r, _e, { storyMapId }) => [
        { type: QueryTags.StoryMap, id: storyMapId },
      ],
    }),

    moveStep: builder.mutation<
      null,
      {
        storyMapId: string
        storyMapKey: string
        stepId: string
        request: MoveStepRequest
      }
    >({
      queryFn: async ({ storyMapId, stepId, request }) => {
        try {
          await getStoryMapsClient().moveStep(storyMapId, stepId, request)
          return { data: null }
        } catch (error) {
          return { error }
        }
      },
      // Re-parent the step, then renumber both goals — the one it left and the one it joined.
      onQueryStarted: async (
        { storyMapKey, stepId, request },
        { dispatch, queryFulfilled },
      ) => {
        const patchResult = dispatch(
          storyMapsApi.util.updateQueryData(
            'getStoryMap',
            storyMapKey,
            (draft) => applyMoveStep(draft, stepId, request),
          ),
        )
        try {
          await queryFulfilled
        } catch {
          patchResult.undo()
        }
      },
      invalidatesTags: (_r, _e, { storyMapId }) => [
        { type: QueryTags.StoryMap, id: storyMapId },
      ],
    }),

    reorderSwimLane: builder.mutation<
      null,
      {
        storyMapId: string
        storyMapKey: string
        swimLaneId: string
        newOrder: number
      }
    >({
      queryFn: async ({ storyMapId, swimLaneId, newOrder }) => {
        try {
          await getStoryMapsClient().reorderSwimLane(storyMapId, swimLaneId, {
            newOrder,
          })
          return { data: null }
        } catch (error) {
          return { error }
        }
      },
      onQueryStarted: async (
        { storyMapKey, swimLaneId, newOrder },
        { dispatch, queryFulfilled },
      ) => {
        const patchResult = dispatch(
          storyMapsApi.util.updateQueryData(
            'getStoryMap',
            storyMapKey,
            (draft) => {
              reorderInPlace(draft.swimLanes, swimLaneId, newOrder)
            },
          ),
        )
        try {
          await queryFulfilled
        } catch {
          patchResult.undo()
        }
      },
      invalidatesTags: (_r, _e, { storyMapId }) => [
        { type: QueryTags.StoryMap, id: storyMapId },
      ],
    }),

    moveTask: builder.mutation<
      null,
      {
        storyMapId: string
        storyMapKey: string
        taskId: string
        request: MoveTaskRequest
      }
    >({
      queryFn: async ({ storyMapId, taskId, request }) => {
        try {
          await getStoryMapsClient().moveTask(storyMapId, taskId, request)
          return { data: null }
        } catch (error) {
          return { error }
        }
      },
      // There is no reorder endpoint for tasks — a same-cell reorder is a move whose step and lane
      // are unchanged. Order is scoped to a cell, so both the old and new cells are renumbered.
      onQueryStarted: async (
        { storyMapKey, taskId, request },
        { dispatch, queryFulfilled },
      ) => {
        const patchResult = dispatch(
          storyMapsApi.util.updateQueryData(
            'getStoryMap',
            storyMapKey,
            (draft) => applyMoveTask(draft, taskId, request),
          ),
        )
        try {
          await queryFulfilled
        } catch {
          patchResult.undo()
        }
      },
      invalidatesTags: (_r, _e, { storyMapId }) => [
        { type: QueryTags.StoryMap, id: storyMapId },
      ],
    }),

    renameStep: builder.mutation<
      null,
      {
        storyMapId: string
        storyMapKey: string
        stepId: string
        request: RenameStepRequest
      }
    >({
      queryFn: async ({ storyMapId, stepId, request }) => {
        try {
          await getStoryMapsClient().renameStep(storyMapId, stepId, request)
          return { data: null }
        } catch (error) {
          return { error }
        }
      },
      onQueryStarted: async (
        { storyMapKey, stepId, request },
        { dispatch, queryFulfilled },
      ) => {
        const patchResult = dispatch(
          storyMapsApi.util.updateQueryData(
            'getStoryMap',
            storyMapKey,
            (draft) => {
              for (const goal of draft.goals) {
                const step = goal.steps.find((s) => s.id === stepId)
                if (step) {
                  step.name = request.name
                  break
                }
              }
            },
          ),
        )
        try {
          await queryFulfilled
        } catch {
          patchResult.undo()
        }
      },
      invalidatesTags: (_r, _e, { storyMapId }) => [
        { type: QueryTags.StoryMap, id: storyMapId },
      ],
    }),

    deleteStep: builder.mutation<
      null,
      { storyMapId: string; storyMapKey: string; stepId: string }
    >({
      queryFn: async ({ storyMapId, stepId }) => {
        try {
          await getStoryMapsClient().deleteStep(storyMapId, stepId)
          return { data: null }
        } catch (error) {
          return { error }
        }
      },
      onQueryStarted: async (
        { storyMapKey, stepId },
        { dispatch, queryFulfilled },
      ) => {
        const patchResult = dispatch(
          storyMapsApi.util.updateQueryData(
            'getStoryMap',
            storyMapKey,
            (draft) => {
              for (const goal of draft.goals) {
                goal.steps = goal.steps.filter((s) => s.id !== stepId)
              }
            },
          ),
        )
        try {
          await queryFulfilled
        } catch {
          patchResult.undo()
        }
      },
      invalidatesTags: (_r, _e, { storyMapId }) => [
        { type: QueryTags.StoryMap, id: storyMapId },
      ],
    }),

    // Title and description have a mutation each rather than sharing the combined updateTask
    // endpoint. The board edits them from two places at once — the card renames inline while the
    // drawer edits notes — and sending both fields from either would revert the other's change.
    // updateTask itself is left on the API for consumers setting both together.

    renameTask: builder.mutation<
      null,
      {
        storyMapId: string
        storyMapKey: string
        taskId: string
        request: RenameTaskRequest
      }
    >({
      queryFn: async ({ storyMapId, taskId, request }) => {
        try {
          await getStoryMapsClient().renameTask(storyMapId, taskId, request)
          return { data: null }
        } catch (error) {
          return { error }
        }
      },
      onQueryStarted: async (
        { storyMapKey, taskId, request },
        { dispatch, queryFulfilled },
      ) => {
        const patchResult = dispatch(
          storyMapsApi.util.updateQueryData(
            'getStoryMap',
            storyMapKey,
            (draft) => {
              const task = findTaskInDraft(draft, taskId)
              if (task) task.title = request.title
            },
          ),
        )
        try {
          await queryFulfilled
        } catch {
          patchResult.undo()
        }
      },
      invalidatesTags: (_r, _e, { storyMapId }) => [
        { type: QueryTags.StoryMap, id: storyMapId },
      ],
    }),

    setTaskDescription: builder.mutation<
      null,
      {
        storyMapId: string
        storyMapKey: string
        taskId: string
        request: SetTaskDescriptionRequest
      }
    >({
      queryFn: async ({ storyMapId, taskId, request }) => {
        try {
          await getStoryMapsClient().setTaskDescription(
            storyMapId,
            taskId,
            request,
          )
          return { data: null }
        } catch (error) {
          return { error }
        }
      },
      onQueryStarted: async (
        { storyMapKey, taskId, request },
        { dispatch, queryFulfilled },
      ) => {
        const patchResult = dispatch(
          storyMapsApi.util.updateQueryData(
            'getStoryMap',
            storyMapKey,
            (draft) => {
              const task = findTaskInDraft(draft, taskId)
              if (task) task.description = request.description
            },
          ),
        )
        try {
          await queryFulfilled
        } catch {
          patchResult.undo()
        }
      },
      invalidatesTags: (_r, _e, { storyMapId }) => [
        { type: QueryTags.StoryMap, id: storyMapId },
      ],
    }),

    deleteTask: builder.mutation<
      null,
      { storyMapId: string; storyMapKey: string; taskId: string }
    >({
      queryFn: async ({ storyMapId, taskId }) => {
        try {
          await getStoryMapsClient().deleteTask(storyMapId, taskId)
          return { data: null }
        } catch (error) {
          return { error }
        }
      },
      onQueryStarted: async (
        { storyMapKey, taskId },
        { dispatch, queryFulfilled },
      ) => {
        const patchResult = dispatch(
          storyMapsApi.util.updateQueryData(
            'getStoryMap',
            storyMapKey,
            (draft) => {
              for (const goal of draft.goals) {
                for (const step of goal.steps) {
                  step.tasks = step.tasks.filter((t) => t.id !== taskId)
                }
              }
            },
          ),
        )
        try {
          await queryFulfilled
        } catch {
          patchResult.undo()
        }
      },
      invalidatesTags: (_r, _e, { storyMapId }) => [
        { type: QueryTags.StoryMap, id: storyMapId },
      ],
    }),

    setStepPersonas: builder.mutation<
      null,
      {
        storyMapId: string
        storyMapKey: string
        stepId: string
        request: SetStepPersonasRequest
      }
    >({
      queryFn: async ({ storyMapId, stepId, request }) => {
        try {
          await getStoryMapsClient().setStepPersonas(
            storyMapId,
            stepId,
            request,
          )
          return { data: null }
        } catch (error) {
          return { error }
        }
      },
      // Retag up front so the dot fills on click without waiting for the round trip.
      onQueryStarted: async (
        { storyMapKey, stepId, request },
        { dispatch, queryFulfilled },
      ) => {
        const patchResult = dispatch(
          storyMapsApi.util.updateQueryData(
            'getStoryMap',
            storyMapKey,
            (draft) => {
              for (const goal of draft.goals) {
                const step = goal.steps.find((s) => s.id === stepId)
                if (step) {
                  step.personaIds = [...request.personaIds]
                  return
                }
              }
            },
          ),
        )
        try {
          await queryFulfilled
        } catch {
          patchResult.undo()
        }
      },
      invalidatesTags: (_r, _e, { storyMapId }) => [
        { type: QueryTags.StoryMap, id: storyMapId },
      ],
    }),

    setTaskPersonas: builder.mutation<
      null,
      {
        storyMapId: string
        storyMapKey: string
        taskId: string
        request: SetTaskPersonasRequest
      }
    >({
      queryFn: async ({ storyMapId, taskId, request }) => {
        try {
          await getStoryMapsClient().setTaskPersonas(
            storyMapId,
            taskId,
            request,
          )
          return { data: null }
        } catch (error) {
          return { error }
        }
      },
      onQueryStarted: async (
        { storyMapKey, taskId, request },
        { dispatch, queryFulfilled },
      ) => {
        const patchResult = dispatch(
          storyMapsApi.util.updateQueryData(
            'getStoryMap',
            storyMapKey,
            (draft) => {
              for (const goal of draft.goals) {
                for (const step of goal.steps) {
                  const task = step.tasks.find((t) => t.id === taskId)
                  if (task) {
                    task.personaIds = [...request.personaIds]
                    return
                  }
                }
              }
            },
          ),
        )
        try {
          await queryFulfilled
        } catch {
          patchResult.undo()
        }
      },
      invalidatesTags: (_r, _e, { storyMapId }) => [
        { type: QueryTags.StoryMap, id: storyMapId },
      ],
    }),

    addSwimLane: builder.mutation<
      StoryMapSwimLaneDto,
      { storyMapId: string; request: AddSwimLaneRequest }
    >({
      queryFn: async ({ storyMapId, request }) => {
        try {
          const data = await getStoryMapsClient().addSwimLane(
            storyMapId,
            request,
          )
          return { data }
        } catch (error) {
          return { error }
        }
      },
      invalidatesTags: (_r, _e, { storyMapId }) => [
        { type: QueryTags.StoryMap, id: storyMapId },
      ],
    }),

    renameSwimLane: builder.mutation<
      null,
      {
        storyMapId: string
        storyMapKey: string
        swimLaneId: string
        request: RenameSwimLaneRequest
      }
    >({
      queryFn: async ({ storyMapId, swimLaneId, request }) => {
        try {
          await getStoryMapsClient().renameSwimLane(
            storyMapId,
            swimLaneId,
            request,
          )
          return { data: null }
        } catch (error) {
          return { error }
        }
      },
      onQueryStarted: async (
        { storyMapKey, swimLaneId, request },
        { dispatch, queryFulfilled },
      ) => {
        const patchResult = dispatch(
          storyMapsApi.util.updateQueryData(
            'getStoryMap',
            storyMapKey,
            (draft) => {
              const lane = draft.swimLanes.find((l) => l.id === swimLaneId)
              if (lane) lane.name = request.name
            },
          ),
        )
        try {
          await queryFulfilled
        } catch {
          patchResult.undo()
        }
      },
      invalidatesTags: (_r, _e, { storyMapId }) => [
        { type: QueryTags.StoryMap, id: storyMapId },
      ],
    }),

    setSwimLaneDates: builder.mutation<
      null,
      {
        storyMapId: string
        storyMapKey: string
        swimLaneId: string
        request: SetSwimLaneDatesRequest
      }
    >({
      queryFn: async ({ storyMapId, swimLaneId, request }) => {
        try {
          await getStoryMapsClient().setSwimLaneDates(
            storyMapId,
            swimLaneId,
            request,
          )
          return { data: null }
        } catch (error) {
          return { error }
        }
      },
      // Both dates are independently optional — clearing one sends undefined for it.
      onQueryStarted: async (
        { storyMapKey, swimLaneId, request },
        { dispatch, queryFulfilled },
      ) => {
        const patchResult = dispatch(
          storyMapsApi.util.updateQueryData(
            'getStoryMap',
            storyMapKey,
            (draft) => {
              const lane = draft.swimLanes.find((l) => l.id === swimLaneId)
              if (lane) {
                lane.startDate = request.startDate
                lane.endDate = request.endDate
              }
            },
          ),
        )
        try {
          await queryFulfilled
        } catch {
          patchResult.undo()
        }
      },
      invalidatesTags: (_r, _e, { storyMapId }) => [
        { type: QueryTags.StoryMap, id: storyMapId },
      ],
    }),

    removeSwimLane: builder.mutation<
      number,
      { storyMapId: string; storyMapKey: string; swimLaneId: string }
    >({
      queryFn: async ({ storyMapId, swimLaneId }) => {
        try {
          // Returns the number of tasks reassigned to the default lane.
          const data = await getStoryMapsClient().removeSwimLane(
            storyMapId,
            swimLaneId,
          )
          return { data }
        } catch (error) {
          return { error }
        }
      },
      // Mirror what the domain does server-side — the lane goes, its tasks move to the default lane
      // — so the row disappears without its tasks flickering away with it.
      onQueryStarted: async (
        { storyMapKey, swimLaneId },
        { dispatch, queryFulfilled },
      ) => {
        const patchResult = dispatch(
          storyMapsApi.util.updateQueryData(
            'getStoryMap',
            storyMapKey,
            (draft) => applyRemoveSwimLane(draft, swimLaneId),
          ),
        )
        try {
          await queryFulfilled
        } catch {
          patchResult.undo()
        }
      },
      invalidatesTags: (_r, _e, { storyMapId }) => [
        { type: QueryTags.StoryMap, id: storyMapId },
      ],
    }),

    // ---- Task checklist. The task's `checklist` array and its two denormalized counts are patched
    // together, so the drawer's list and the card's badge never disagree mid-flight. ----

    addChecklistItem: builder.mutation<
      StoryMapTaskDto,
      {
        storyMapId: string
        storyMapKey: string
        taskId: string
        request: AddChecklistItemRequest
      }
    >({
      queryFn: async ({ storyMapId, taskId, request }) => {
        try {
          const data = await getStoryMapsClient().addChecklistItem(
            storyMapId,
            taskId,
            request,
          )
          return { data }
        } catch (error) {
          return { error }
        }
      },
      // Insert under a temporary id so the row appears as soon as the user presses Enter, then swap
      // in the server's real id — checking or deleting the row before it resolves would otherwise
      // send a temp id the server has never seen.
      onQueryStarted: async (
        { storyMapKey, taskId, request },
        { dispatch, queryFulfilled },
      ) => {
        const itemTempId = tempId()
        const patchResult = dispatch(
          storyMapsApi.util.updateQueryData(
            'getStoryMap',
            storyMapKey,
            (draft) => {
              const task = findTaskInDraft(draft, taskId)
              if (!task) return
              const nextOrder = task.checklist.length
                ? Math.max(...task.checklist.map((i) => i.order)) + 1
                : 0
              task.checklist.push({
                id: itemTempId,
                name: request.name,
                isChecked: false,
                order: nextOrder,
              })
              recountChecklist(task)
            },
          ),
        )
        try {
          const { data: updated } = await queryFulfilled
          dispatch(
            storyMapsApi.util.updateQueryData(
              'getStoryMap',
              storyMapKey,
              (draft) => {
                const task = findTaskInDraft(draft, taskId)
                if (!task) return
                // The command returns the whole task, so take its checklist as authoritative rather
                // than guessing which server id belongs to the temporary row.
                task.checklist = [...updated.checklist]
                recountChecklist(task)
              },
            ),
          )
        } catch {
          patchResult.undo()
        }
      },
      invalidatesTags: (_r, _e, { storyMapId }) => [
        { type: QueryTags.StoryMap, id: storyMapId },
      ],
    }),

    renameChecklistItem: builder.mutation<
      null,
      {
        storyMapId: string
        storyMapKey: string
        taskId: string
        itemId: string
        request: RenameChecklistItemRequest
      }
    >({
      queryFn: async ({ storyMapId, taskId, itemId, request }) => {
        try {
          await getStoryMapsClient().renameChecklistItem(
            storyMapId,
            taskId,
            itemId,
            request,
          )
          return { data: null }
        } catch (error) {
          return { error }
        }
      },
      onQueryStarted: async (
        { storyMapKey, taskId, itemId, request },
        { dispatch, queryFulfilled },
      ) => {
        const patchResult = dispatch(
          storyMapsApi.util.updateQueryData(
            'getStoryMap',
            storyMapKey,
            (draft) => {
              const item = findTaskInDraft(draft, taskId)?.checklist.find(
                (i) => i.id === itemId,
              )
              if (item) item.name = request.name
            },
          ),
        )
        try {
          await queryFulfilled
        } catch {
          patchResult.undo()
        }
      },
      invalidatesTags: (_r, _e, { storyMapId }) => [
        { type: QueryTags.StoryMap, id: storyMapId },
      ],
    }),

    setChecklistItemChecked: builder.mutation<
      null,
      {
        storyMapId: string
        storyMapKey: string
        taskId: string
        itemId: string
        request: SetChecklistItemCheckedRequest
      }
    >({
      queryFn: async ({ storyMapId, taskId, itemId, request }) => {
        try {
          await getStoryMapsClient().setChecklistItemChecked(
            storyMapId,
            taskId,
            itemId,
            request,
          )
          return { data: null }
        } catch (error) {
          return { error }
        }
      },
      // The checkbox must fill on click, not a round trip later.
      onQueryStarted: async (
        { storyMapKey, taskId, itemId, request },
        { dispatch, queryFulfilled },
      ) => {
        const patchResult = dispatch(
          storyMapsApi.util.updateQueryData(
            'getStoryMap',
            storyMapKey,
            (draft) => {
              const task = findTaskInDraft(draft, taskId)
              const item = task?.checklist.find((i) => i.id === itemId)
              if (!task || !item) return
              item.isChecked = request.isChecked
              recountChecklist(task)
            },
          ),
        )
        try {
          await queryFulfilled
        } catch {
          patchResult.undo()
        }
      },
      invalidatesTags: (_r, _e, { storyMapId }) => [
        { type: QueryTags.StoryMap, id: storyMapId },
      ],
    }),

    removeChecklistItem: builder.mutation<
      null,
      {
        storyMapId: string
        storyMapKey: string
        taskId: string
        itemId: string
      }
    >({
      queryFn: async ({ storyMapId, taskId, itemId }) => {
        try {
          await getStoryMapsClient().removeChecklistItem(
            storyMapId,
            taskId,
            itemId,
          )
          return { data: null }
        } catch (error) {
          return { error }
        }
      },
      onQueryStarted: async (
        { storyMapKey, taskId, itemId },
        { dispatch, queryFulfilled },
      ) => {
        const patchResult = dispatch(
          storyMapsApi.util.updateQueryData(
            'getStoryMap',
            storyMapKey,
            (draft) => {
              const task = findTaskInDraft(draft, taskId)
              if (!task) return
              task.checklist = task.checklist.filter((i) => i.id !== itemId)
              // Order is contiguous server-side, so close the gap the removal left.
              task.checklist
                .sort((a, b) => a.order - b.order)
                .forEach((item, index) => {
                  item.order = index
                })
              recountChecklist(task)
            },
          ),
        )
        try {
          await queryFulfilled
        } catch {
          patchResult.undo()
        }
      },
      invalidatesTags: (_r, _e, { storyMapId }) => [
        { type: QueryTags.StoryMap, id: storyMapId },
      ],
    }),

    // ---- Linked work item ----

    linkWorkItem: builder.mutation<
      null,
      {
        storyMapId: string
        storyMapKey: string
        taskId: string
        request: LinkWorkItemRequest
      }
    >({
      queryFn: async ({ storyMapId, taskId, request }) => {
        try {
          await getStoryMapsClient().linkWorkItem(storyMapId, taskId, request)
          return { data: null }
        } catch (error) {
          return { error }
        }
      },
      onQueryStarted: async (
        { storyMapKey, taskId, request },
        { dispatch, queryFulfilled },
      ) => {
        const patchResult = dispatch(
          storyMapsApi.util.updateQueryData(
            'getStoryMap',
            storyMapKey,
            (draft) => {
              const task = findTaskInDraft(draft, taskId)
              if (task) task.linkedWorkItemId = request.workItemId
            },
          ),
        )
        try {
          await queryFulfilled
        } catch {
          patchResult.undo()
        }
      },
      invalidatesTags: (_r, _e, { storyMapId }) => [
        { type: QueryTags.StoryMap, id: storyMapId },
      ],
    }),

    unlinkWorkItem: builder.mutation<
      null,
      { storyMapId: string; storyMapKey: string; taskId: string }
    >({
      queryFn: async ({ storyMapId, taskId }) => {
        try {
          await getStoryMapsClient().unlinkWorkItem(storyMapId, taskId)
          return { data: null }
        } catch (error) {
          return { error }
        }
      },
      onQueryStarted: async (
        { storyMapKey, taskId },
        { dispatch, queryFulfilled },
      ) => {
        const patchResult = dispatch(
          storyMapsApi.util.updateQueryData(
            'getStoryMap',
            storyMapKey,
            (draft) => {
              const task = findTaskInDraft(draft, taskId)
              if (task) task.linkedWorkItemId = undefined
            },
          ),
        )
        try {
          await queryFulfilled
        } catch {
          patchResult.undo()
        }
      },
      invalidatesTags: (_r, _e, { storyMapId }) => [
        { type: QueryTags.StoryMap, id: storyMapId },
      ],
    }),

    addPersona: builder.mutation<
      StoryMapPersonaDto,
      { storyMapId: string; storyMapKey: string; request: AddPersonaRequest }
    >({
      queryFn: async ({ storyMapId, request }) => {
        try {
          const data = await getStoryMapsClient().addPersona(
            storyMapId,
            request,
          )
          return { data }
        } catch (error) {
          return { error }
        }
      },
      // Insert the persona into the cached map up front so the chip appears instantly, then swap the
      // temporary entry for the server's real one when the request resolves (the invalidatesTags
      // refetch below still reconciles against any concurrent SignalR edits). Roll back on failure.
      onQueryStarted: async (
        { storyMapKey, request },
        { dispatch, queryFulfilled },
      ) => {
        const personaTempId = tempId()
        const patchResult = dispatch(
          storyMapsApi.util.updateQueryData(
            'getStoryMap',
            storyMapKey,
            (draft) => {
              // New personas land at the end; the user can drag to reorder afterwards.
              const nextOrder = draft.personas.length
                ? Math.max(...draft.personas.map((p) => p.order)) + 1
                : 0
              draft.personas.push({
                id: personaTempId,
                name: request.name,
                description: request.description,
                color: request.color,
                order: nextOrder,
              })
            },
          ),
        )
        try {
          const { data: created } = await queryFulfilled
          dispatch(
            storyMapsApi.util.updateQueryData(
              'getStoryMap',
              storyMapKey,
              (draft) => {
                const temp = draft.personas.find((p) => p.id === personaTempId)
                if (temp) {
                  temp.id = created.id
                  temp.name = created.name
                  temp.description = created.description
                  temp.color = created.color
                  temp.order = created.order
                }
              },
            ),
          )
        } catch {
          patchResult.undo()
        }
      },
      invalidatesTags: (_r, _e, { storyMapId }) => [
        { type: QueryTags.StoryMap, id: storyMapId },
      ],
    }),

    updatePersona: builder.mutation<
      null,
      {
        storyMapId: string
        storyMapKey: string
        personaId: string
        request: UpdatePersonaRequest
      }
    >({
      queryFn: async ({ storyMapId, personaId, request }) => {
        try {
          await getStoryMapsClient().updatePersona(
            storyMapId,
            personaId,
            request,
          )
          // RTK Query requires a defined `data` value; a void endpoint returns null, not undefined.
          return { data: null }
        } catch (error) {
          return { error }
        }
      },
      // Patch the persona in the cached map up front so the edit shows immediately. Roll back on
      // failure; the invalidatesTags refetch reconciles against any concurrent SignalR edits.
      onQueryStarted: async (
        { storyMapKey, personaId, request },
        { dispatch, queryFulfilled },
      ) => {
        const patchResult = dispatch(
          storyMapsApi.util.updateQueryData(
            'getStoryMap',
            storyMapKey,
            (draft) => {
              const persona = draft.personas.find((p) => p.id === personaId)
              if (persona) {
                persona.name = request.name
                persona.description = request.description
                persona.color = request.color
              }
            },
          ),
        )
        try {
          await queryFulfilled
        } catch {
          patchResult.undo()
        }
      },
      invalidatesTags: (_r, _e, { storyMapId }) => [
        { type: QueryTags.StoryMap, id: storyMapId },
      ],
    }),

    deletePersona: builder.mutation<
      number,
      { storyMapId: string; storyMapKey: string; personaId: string }
    >({
      queryFn: async ({ storyMapId, personaId }) => {
        try {
          const data = await getStoryMapsClient().deletePersona(
            storyMapId,
            personaId,
          )
          return { data }
        } catch (error) {
          return { error }
        }
      },
      // Remove the persona from the cached map (and strip its tag from every node) up front. Roll
      // back on failure; the invalidatesTags refetch reconciles the authoritative graph.
      onQueryStarted: async (
        { storyMapKey, personaId },
        { dispatch, queryFulfilled },
      ) => {
        const patchResult = dispatch(
          storyMapsApi.util.updateQueryData(
            'getStoryMap',
            storyMapKey,
            (draft) => {
              draft.personas = draft.personas.filter((p) => p.id !== personaId)
              const strip = (ids: string[]) =>
                ids.filter((id) => id !== personaId)
              for (const goal of draft.goals) {
                goal.personaIds = strip(goal.personaIds)
                for (const step of goal.steps) {
                  step.personaIds = strip(step.personaIds)
                  for (const task of step.tasks) {
                    task.personaIds = strip(task.personaIds)
                  }
                }
              }
            },
          ),
        )
        try {
          await queryFulfilled
        } catch {
          patchResult.undo()
        }
      },
      invalidatesTags: (_r, _e, { storyMapId }) => [
        { type: QueryTags.StoryMap, id: storyMapId },
      ],
    }),

    reorderPersona: builder.mutation<
      null,
      {
        storyMapId: string
        storyMapKey: string
        personaId: string
        newOrder: number
      }
    >({
      queryFn: async ({ storyMapId, personaId, newOrder }) => {
        try {
          await getStoryMapsClient().reorderPersona(storyMapId, personaId, {
            newOrder,
          })
          // RTK Query requires a defined `data` value; a void endpoint returns null, not undefined.
          return { data: null }
        } catch (error) {
          return { error }
        }
      },
      // Move the persona to its new position and renumber contiguously in the cache so the filter
      // bar and manage list reflect the new order instantly. Roll back on failure.
      onQueryStarted: async (
        { storyMapKey, personaId, newOrder },
        { dispatch, queryFulfilled },
      ) => {
        const patchResult = dispatch(
          storyMapsApi.util.updateQueryData(
            'getStoryMap',
            storyMapKey,
            (draft) => {
              reorderInPlace(draft.personas, personaId, newOrder)
            },
          ),
        )
        try {
          await queryFulfilled
        } catch {
          patchResult.undo()
        }
      },
      invalidatesTags: (_r, _e, { storyMapId }) => [
        { type: QueryTags.StoryMap, id: storyMapId },
      ],
    }),
  }),
})

export const {
  useGetStoryMapsQuery,
  useGetStoryMapQuery,
  useCreateStoryMapMutation,
  useUpdateStoryMapMutation,
  useArchiveStoryMapMutation,
  useDeleteStoryMapMutation,
  useAddGoalMutation,
  useReorderGoalMutation,
  useReorderStepMutation,
  useMoveStepMutation,
  useMoveTaskMutation,
  useReorderSwimLaneMutation,
  useRenameGoalMutation,
  useDeleteGoalMutation,
  useAddStepMutation,
  useRenameStepMutation,
  useDeleteStepMutation,
  useAddTaskMutation,
  useRenameTaskMutation,
  useSetTaskDescriptionMutation,
  useDeleteTaskMutation,
  useSetStepPersonasMutation,
  useSetTaskPersonasMutation,
  useAddChecklistItemMutation,
  useRenameChecklistItemMutation,
  useSetChecklistItemCheckedMutation,
  useRemoveChecklistItemMutation,
  useLinkWorkItemMutation,
  useUnlinkWorkItemMutation,
  useAddSwimLaneMutation,
  useRenameSwimLaneMutation,
  useSetSwimLaneDatesMutation,
  useRemoveSwimLaneMutation,
  useAddPersonaMutation,
  useUpdatePersonaMutation,
  useDeletePersonaMutation,
  useReorderPersonaMutation,
} = storyMapsApi

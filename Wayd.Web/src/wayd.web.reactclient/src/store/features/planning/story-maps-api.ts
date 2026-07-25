import { getStoryMapsClient } from '@/src/services/clients'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import {
  AddGoalRequest,
  AddSwimLaneRequest,
  AddPersonaRequest,
  AddStepRequest,
  AddTaskRequest,
  CreateStoryMapRequest,
  ObjectIdAndKey,
  RenameGoalRequest,
  RenameStepRequest,
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
  UpdateTaskRequest,
} from '@/src/services/wayd-api'

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
      invalidatesTags: (_r, _e, { id }) => [
        { type: QueryTags.StoryMap, id },
        { type: QueryTags.StoryMapList, id: 'LIST' },
      ],
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

    updateTask: builder.mutation<
      null,
      {
        storyMapId: string
        storyMapKey: string
        taskId: string
        request: UpdateTaskRequest
      }
    >({
      queryFn: async ({ storyMapId, taskId, request }) => {
        try {
          await getStoryMapsClient().updateTask(storyMapId, taskId, request)
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
                    task.title = request.title
                    task.description = request.description
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
          await getStoryMapsClient().setStepPersonas(storyMapId, stepId, request)
          return { data: null }
        } catch (error) {
          return { error }
        }
      },
      // Retag the step in the cache up front so the dot fills/empties on click without waiting for
      // the round trip. Roll back on failure.
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
          await getStoryMapsClient().setTaskPersonas(storyMapId, taskId, request)
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
          const data = await getStoryMapsClient().addSwimLane(storyMapId, request)
          return { data }
        } catch (error) {
          return { error }
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
          const data = await getStoryMapsClient().addPersona(storyMapId, request)
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
        const tempId = `temp-${crypto.randomUUID()}`
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
                id: tempId,
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
                const temp = draft.personas.find((p) => p.id === tempId)
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
              const strip = (ids: string[]) => ids.filter((id) => id !== personaId)
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
              const ordered = [...draft.personas].sort(
                (a, b) => a.order - b.order,
              )
              const from = ordered.findIndex((p) => p.id === personaId)
              if (from === -1) return
              const [moved] = ordered.splice(from, 1)
              const to = Math.max(0, Math.min(newOrder, ordered.length))
              ordered.splice(to, 0, moved)
              ordered.forEach((p, i) => {
                const persona = draft.personas.find((x) => x.id === p.id)
                if (persona) persona.order = i
              })
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
  useRenameGoalMutation,
  useDeleteGoalMutation,
  useAddStepMutation,
  useRenameStepMutation,
  useDeleteStepMutation,
  useAddTaskMutation,
  useUpdateTaskMutation,
  useDeleteTaskMutation,
  useSetStepPersonasMutation,
  useSetTaskPersonasMutation,
  useAddSwimLaneMutation,
  useAddPersonaMutation,
  useUpdatePersonaMutation,
  useDeletePersonaMutation,
  useReorderPersonaMutation,
} = storyMapsApi

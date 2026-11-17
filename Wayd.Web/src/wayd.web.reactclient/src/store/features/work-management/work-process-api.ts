import { workProcessesGetList } from '@/src/services/api/work-processes'
import {
  workProcessesActivate,
  workProcessesDeactivate,
  workProcessesGet,
  workProcessesGetActivities,
  workProcessesGetSchemes,
} from '@/src/services/api/work-processes'
import {
  WorkProcessDto,
  WorkProcessListDto,
  WorkProcessSchemeDto,
  PagedResponseOfActivityLogDto,
} from '@/src/services/api/wayd.schemas'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'

// function providesList<R extends { id: string | number }[], T extends string>(
//   resultsWithIds: R | undefined,
//   tagType: T,
// ) {
//   return resultsWithIds
//     ? [
//         { type: tagType, id: 'LIST' },
//         ...resultsWithIds.map(({ id }) => ({ type: tagType, id })),
//       ]
//     : [{ type: tagType, id: 'LIST' }]
// }

export interface ChangeWorkProcessIsActiveMutationRequest {
  id: string
  isActive: boolean
}

export const workProcessApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getWorkProcesses: builder.query<WorkProcessListDto[], boolean>({
      queryFn: async (includeInactive) => {
        try {
          const data = await workProcessesGetList({ includeInactive })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      // TODO: why is the id on the model optional
      //providesTags: (result, error, arg) => providesList(result, QueryTags.WorkProcess),
      providesTags: (result) => [
        QueryTags.WorkProcess,
        ...(result?.map(({ id }) => ({ type: QueryTags.WorkProcess, id })) ??
          []),
      ],
    }),
    getWorkProcess: builder.query<WorkProcessDto | null, string>({
      queryFn: async (idOrKey: string) => {
        try {
          const data = await workProcessesGet(idOrKey)
          return { data }
        } catch (error: any) {
          // Return null for 404 so callers can render their own empty/not-found
          // state without polluting the console. Happens when an AzDO connection
          // still references a Wayd.Work.WorkProcess that no longer exists.
          if (error?.status === 404) {
            return { data: null }
          }
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.WorkProcess, id: arg }, // typically arg is the key
      ],
    }),
    changeWorkProcessIsActive: builder.mutation<
      null,
      ChangeWorkProcessIsActiveMutationRequest
    >({
      queryFn: async ({ id, isActive }) => {
        try {
          if (isActive) {
            await workProcessesActivate(id)
          } else {
            await workProcessesDeactivate(id)
          }
          return { data: null }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => {
        return [{ type: QueryTags.WorkProcess, id: arg.id }]
      },
    }),
    getWorkProcessSchemes: builder.query<WorkProcessSchemeDto[], string>({
      queryFn: async (workProcessId: string) => {
        try {
          const data = await workProcessesGetSchemes(workProcessId)

          data.sort((a, b) => a.workType.name.localeCompare(b.workType.name))

          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.WorkProcessScheme, id: arg },
        ...(result?.map(({ id }) => ({
          type: QueryTags.WorkProcessScheme,
          id,
        })) ?? []),
      ],
    }),

    getWorkProcessActivities: builder.query<
      PagedResponseOfActivityLogDto,
      { idOrKey: string | number; page?: number; pageSize?: number }
    >({
      queryFn: async ({ idOrKey, page, pageSize }) => {
        try {
          const data = await workProcessesGetActivities(String(idOrKey), {
            page: page,
            pageSize: pageSize,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, { idOrKey }) => [
        { type: QueryTags.ActivityLog, id: String(idOrKey) },
      ],
    }),
  }),
  overrideExisting: false,
})

export const {
  useGetWorkProcessesQuery,
  useGetWorkProcessQuery,
  useChangeWorkProcessIsActiveMutation,
  useGetWorkProcessSchemesQuery,
  useGetWorkProcessActivitiesQuery,
  useLazyGetWorkProcessActivitiesQuery,
} = workProcessApi

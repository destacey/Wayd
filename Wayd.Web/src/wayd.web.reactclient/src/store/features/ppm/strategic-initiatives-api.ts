import {
  PagedResponseOfActivityLogDto,
  ManageStrategicInitiativeProjectsRequest,
  ProjectListDto,
} from '@/src/services/api/wayd.schemas'
import { apiSlice } from './../apiSlice'
import { QueryTags } from '../query-tags'
import { ppmActivityTag } from './ppm-activity-tags'
import {
  strategicInitiativesActivate,
  strategicInitiativesApprove,
  strategicInitiativesCancel,
  strategicInitiativesComplete,
  strategicInitiativesCreate,
  strategicInitiativesDelete,
  strategicInitiativesGetActivities,
  strategicInitiativesGetProjects,
  strategicInitiativesGetStrategicInitiative,
  strategicInitiativesGetStrategicInitiativeStatuses,
  strategicInitiativesGetStrategicInitiatives,
  strategicInitiativesManageProjects,
  strategicInitiativesUpdate,
} from '@/src/services/api/strategic-initiatives'
import {
  CreateStrategicInitiativeRequest,
  ObjectIdAndKey,
  StrategicInitiativeDetailsDto,
  StrategicInitiativeListDto,
  StrategicInitiativeStatus,
  UpdateStrategicInitiativeRequest,
} from '@/src/services/api/wayd.schemas'
import { StatusOptionModel } from '@/src/components/types'

export const strategicInitiativesApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getStrategicInitiatives: builder.query<
      StrategicInitiativeListDto[],
      { status?: StrategicInitiativeStatus[]; portfolioId?: string } | undefined
    >({
      queryFn: async (request = undefined) => {
        try {
          const data = await strategicInitiativesGetStrategicInitiatives({
            status:
              (request?.status?.length ?? 0) > 0 ? request!.status : undefined,
            portfolioId: request?.portfolioId,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [{ type: QueryTags.StrategicInitiative, id: 'LIST' }],
    }),
    getStrategicInitiative: builder.query<
      StrategicInitiativeDetailsDto,
      number
    >({
      queryFn: async (key) => {
        try {
          const data = await strategicInitiativesGetStrategicInitiative(
            key.toString(),
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.StrategicInitiative, id: arg },
      ],
    }),
    createStrategicInitiative: builder.mutation<
      ObjectIdAndKey,
      CreateStrategicInitiativeRequest
    >({
      queryFn: async (request) => {
        try {
          const data = await strategicInitiativesCreate(request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => {
        return [
          { type: QueryTags.StrategicInitiative, id: 'LIST' },
          { type: QueryTags.PortfolioStrategicInitiatives, id: 'LIST' },
        ]
      },
    }),
    updateStrategicInitiative: builder.mutation<
      void,
      { request: UpdateStrategicInitiativeRequest; cacheKey: number }
    >({
      queryFn: async ({ request, cacheKey }) => {
        try {
          const data = await strategicInitiativesUpdate(request.id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { request, cacheKey }) => {
        return [
          { type: QueryTags.StrategicInitiative, id: 'LIST' },
          { type: QueryTags.StrategicInitiative, id: cacheKey },
          { type: QueryTags.PortfolioStrategicInitiatives, id: 'LIST' },
          ppmActivityTag(request.id),
        ]
      },
    }),
    approveStrategicInitiative: builder.mutation<
      void,
      { id: string; cacheKey: number }
    >({
      queryFn: async ({ id }) => {
        try {
          const data = await strategicInitiativesApprove(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { id, cacheKey }) => {
        return [
          { type: QueryTags.StrategicInitiative, id: 'LIST' },
          { type: QueryTags.StrategicInitiative, id: cacheKey },
          { type: QueryTags.PortfolioStrategicInitiatives, id: 'LIST' },
          ppmActivityTag(id),
        ]
      },
    }),
    activateStrategicInitiative: builder.mutation<
      void,
      { id: string; cacheKey: number }
    >({
      queryFn: async ({ id }) => {
        try {
          const data = await strategicInitiativesActivate(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { id, cacheKey }) => {
        return [
          { type: QueryTags.StrategicInitiative, id: 'LIST' },
          { type: QueryTags.StrategicInitiative, id: cacheKey },
          { type: QueryTags.PortfolioStrategicInitiatives, id: 'LIST' },
          ppmActivityTag(id),
        ]
      },
    }),
    completeStrategicInitiative: builder.mutation<
      void,
      { id: string; cacheKey: number }
    >({
      queryFn: async ({ id }) => {
        try {
          const data = await strategicInitiativesComplete(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { id, cacheKey }) => {
        return [
          { type: QueryTags.StrategicInitiative, id: 'LIST' },
          { type: QueryTags.StrategicInitiative, id: cacheKey },
          { type: QueryTags.PortfolioStrategicInitiatives, id: 'LIST' },
          ppmActivityTag(id),
        ]
      },
    }),
    cancelStrategicInitiative: builder.mutation<
      void,
      { id: string; cacheKey: number }
    >({
      queryFn: async ({ id }) => {
        try {
          const data = await strategicInitiativesCancel(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { id, cacheKey }) => {
        return [
          { type: QueryTags.StrategicInitiative, id: 'LIST' },
          { type: QueryTags.StrategicInitiative, id: cacheKey },
          { type: QueryTags.PortfolioStrategicInitiatives, id: 'LIST' },
          ppmActivityTag(id),
        ]
      },
    }),
    deleteStrategicInitiative: builder.mutation<void, { id: string }>({
      queryFn: async ({ id }) => {
        try {
          const data = await strategicInitiativesDelete(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => {
        return [
          { type: QueryTags.StrategicInitiative, id: 'LIST' },
          { type: QueryTags.PortfolioStrategicInitiatives, id: 'LIST' },
        ]
      },
    }),
    getStrategicInitiativeStatusOptions: builder.query<
      StatusOptionModel<StrategicInitiativeStatus>[],
      void
    >({
      queryFn: async () => {
        try {
          const statuses =
            await strategicInitiativesGetStrategicInitiativeStatuses()

          const data: StatusOptionModel<StrategicInitiativeStatus>[] = statuses
            .sort((a, b) => a.order - b.order)
            .map((s) => ({
              value: s.code,
              label: s.name,
              lifecycleCategory: s.lifecycleCategory,
            }))

          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [QueryTags.StrategicInitiativeStatusOptions],
    }),

    getStrategicInitiativeProjects: builder.query<ProjectListDto[], string>({
      queryFn: async (idOrKey) => {
        try {
          const data = await strategicInitiativesGetProjects(idOrKey)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.StrategicInitiativeProject, id: 'LIST' },
        { type: QueryTags.StrategicInitiativeProject, id: arg },
      ],
    }),
    manageStrategicInitiativeProjects: builder.mutation<
      void,
      ManageStrategicInitiativeProjectsRequest
    >({
      queryFn: async (request) => {
        try {
          const data = await strategicInitiativesManageProjects(
            request.id,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.StrategicInitiativeProject, id: 'LIST' },
        { type: QueryTags.StrategicInitiativeProject, id: arg.id },
        ppmActivityTag(arg.id),
      ],
    }),
    getStrategicInitiativeActivities: builder.query<
      PagedResponseOfActivityLogDto,
      { idOrKey: string | number; page?: number; pageSize?: number }
    >({
      queryFn: async ({ idOrKey, page, pageSize }) => {
        try {
          const data = await strategicInitiativesGetActivities(
            String(idOrKey),
            { page: page, pageSize: pageSize },
          )
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
})

export const {
  useGetStrategicInitiativesQuery,
  useGetStrategicInitiativeQuery,
  useCreateStrategicInitiativeMutation,
  useUpdateStrategicInitiativeMutation,
  useApproveStrategicInitiativeMutation,
  useActivateStrategicInitiativeMutation,
  useCompleteStrategicInitiativeMutation,
  useCancelStrategicInitiativeMutation,
  useDeleteStrategicInitiativeMutation,
  useGetStrategicInitiativeStatusOptionsQuery,
  useGetStrategicInitiativeProjectsQuery,
  useManageStrategicInitiativeProjectsMutation,
  useGetStrategicInitiativeActivitiesQuery,
  useLazyGetStrategicInitiativeActivitiesQuery,
} = strategicInitiativesApi

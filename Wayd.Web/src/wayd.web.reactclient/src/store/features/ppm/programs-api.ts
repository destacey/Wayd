import { getProgramsClient } from '@/src/services/clients'
import { apiSlice } from '../apiSlice'
import {
  CreateProgramRequest,
  ObjectIdAndKey,
  ProgramListDto,
  ProgramDetailsDto,
  UpdateProgramRequest,
  ProjectListDto,
} from '@/src/services/wayd-api'
import { QueryTags } from '../query-tags'
import { BaseOptionType } from 'antd/es/select'
import { StatusOptionModel } from '@/src/components/types'

export const programsApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getPrograms: builder.query<
      ProgramListDto[],
      { status?: number[]; portfolioId?: string } | undefined
    >({
      queryFn: async (request = undefined) => {
        try {
          const data = await getProgramsClient().getPrograms(
            (request?.status?.length ?? 0) > 0 ? request!.status : undefined,
            request?.portfolioId,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [{ type: QueryTags.Program, id: 'LIST' }],
    }),
    getProgram: builder.query<ProgramDetailsDto, number>({
      queryFn: async (key) => {
        try {
          const data = await getProgramsClient().getProgram(key.toString())
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.Program, id: arg },
      ],
    }),
    createProgram: builder.mutation<ObjectIdAndKey, CreateProgramRequest>({
      queryFn: async (request) => {
        try {
          const data = await getProgramsClient().create(request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => {
        return [
          { type: QueryTags.Program, id: 'LIST' },
          { type: QueryTags.PortfolioPrograms, id: 'LIST' },
        ]
      },
    }),
    updateProgram: builder.mutation<
      void,
      { request: UpdateProgramRequest; cacheKey: number }
    >({
      queryFn: async ({ request }) => {
        try {
          const data = await getProgramsClient().update(request.id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { cacheKey }) => {
        return [
          { type: QueryTags.Program, id: 'LIST' },
          { type: QueryTags.Program, id: cacheKey },
          { type: QueryTags.PortfolioPrograms, id: 'LIST' },
        ]
      },
    }),
    activateProgram: builder.mutation<void, { id: string; cacheKey: number }>({
      queryFn: async ({ id }) => {
        try {
          const data = await getProgramsClient().activate(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { cacheKey }) => {
        return [
          { type: QueryTags.Program, id: 'LIST' },
          { type: QueryTags.Program, id: cacheKey },
          { type: QueryTags.PortfolioPrograms, id: 'LIST' },
        ]
      },
    }),
    completeProgram: builder.mutation<void, { id: string; cacheKey: number }>({
      queryFn: async ({ id }) => {
        try {
          const data = await getProgramsClient().complete(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { cacheKey }) => {
        return [
          { type: QueryTags.Program, id: 'LIST' },
          { type: QueryTags.Program, id: cacheKey },
          { type: QueryTags.PortfolioPrograms, id: 'LIST' },
        ]
      },
    }),
    cancelProgram: builder.mutation<void, { id: string; cacheKey: number }>({
      queryFn: async ({ id }) => {
        try {
          const data = await getProgramsClient().cancel(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { cacheKey }) => {
        return [
          { type: QueryTags.Program, id: 'LIST' },
          { type: QueryTags.Program, id: cacheKey },
          { type: QueryTags.PortfolioPrograms, id: 'LIST' },
        ]
      },
    }),
    deleteProgram: builder.mutation<void, string>({
      queryFn: async (id) => {
        try {
          const data = await getProgramsClient().delete(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => {
        return [
          { type: QueryTags.Program, id: 'LIST' },
          { type: QueryTags.PortfolioPrograms, id: 'LIST' },
        ]
      },
    }),
    getProgramProjects: builder.query<
      ProjectListDto[],
      { programIdOrKey: string; status?: number[] }
    >({
      queryFn: async ({ programIdOrKey, status }) => {
        try {
          const data = await getProgramsClient().getProjects(
            programIdOrKey,
            (status?.length ?? 0) > 0 ? status : null,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, { programIdOrKey }) => [
        { type: QueryTags.ProgramProjects, id: 'LIST' },
        { type: QueryTags.ProgramProjects, id: programIdOrKey },
      ],
    }),
    getProgramStatusOptions: builder.query<StatusOptionModel[], void>({
      queryFn: async () => {
        try {
          const statuses = await getProgramsClient().getProgramStatuses()

          const data: StatusOptionModel[] = statuses
            .sort((a, b) => a.order - b.order)
            .map((s) => ({
              value: s.id,
              label: s.name,
              lifecycleCategory: s.lifecycleCategory,
            }))

          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [QueryTags.ProgramStatusOptions],
    }),

    getProgramOptions: builder.query<BaseOptionType[], void>({
      queryFn: async () => {
        try {
          const programs = await getProgramsClient().getPrograms(undefined)

          const data: BaseOptionType[] = programs
            .sort((a, b) => a.name.localeCompare(b.name))
            .map((program) => ({
              label: program.name,
              value: program.id,
            }))

          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
    }),
  }),
})

export const {
  useGetProgramsQuery,
  useGetProgramQuery,
  useCreateProgramMutation,
  useUpdateProgramMutation,
  useActivateProgramMutation,
  useCompleteProgramMutation,
  useCancelProgramMutation,
  useDeleteProgramMutation,
  useGetProgramProjectsQuery,
  useGetProgramOptionsQuery,
  useGetProgramStatusOptionsQuery,
} = programsApi

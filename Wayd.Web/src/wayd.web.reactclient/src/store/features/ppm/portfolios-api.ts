import {
  portfoliosActivate,
  portfoliosArchive,
  portfoliosAssignScoringModel,
  portfoliosClearScoringModel,
  portfoliosClose,
  portfoliosCreate,
  portfoliosDelete,
  portfoliosGetActivities,
  portfoliosGetPortfolio,
  portfoliosGetPortfolioOptions,
  portfoliosGetPortfolioStatuses,
  portfoliosGetPortfolios,
  portfoliosGetPrograms,
  portfoliosGetProjects,
  portfoliosGetRankingScoreboard,
  portfoliosGetStrategicInitiatives,
  portfoliosMoveProjectRanks,
  portfoliosRebalanceProjectRanks,
  portfoliosUpdate,
} from '@/src/services/api/portfolios'
import { apiSlice } from '../apiSlice'
import {
  CreatePortfolioRequest,
  MoveProjectRanksRequest,
  ObjectIdAndKey,
  PortfolioRankingScoreboardDto,
  ProgramListDto,
  ProjectListDto,
  ProjectPortfolioDetailsDto,
  ProjectPortfolioListDto,
  StrategicInitiativeListDto,
  UpdatePortfolioRequest,
  PagedResponseOfActivityLogDto,
  ProgramStatus,
  ProjectPortfolioStatus,
  ProjectStatus,
  StrategicInitiativeStatus,
} from '@/src/services/api/wayd.schemas'
import { QueryTags } from '../query-tags'
import { ppmActivityTag } from './ppm-activity-tags'
import type { BaseOptionType } from 'antd/es/select'
import { OptionModel } from '@/src/components/types'

export const portfoliosApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getPortfolios: builder.query<
      ProjectPortfolioListDto[],
      ProjectPortfolioStatus[] | undefined
    >({
      queryFn: async (portfolioState = undefined) => {
        try {
          const data = await portfoliosGetPortfolios({ status: portfolioState })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [{ type: QueryTags.Portfolio, id: 'LIST' }],
    }),
    getPortfolio: builder.query<ProjectPortfolioDetailsDto, number>({
      queryFn: async (key) => {
        try {
          const data = await portfoliosGetPortfolio(key.toString())
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.Portfolio, id: arg },
      ],
    }),
    createPortfolio: builder.mutation<ObjectIdAndKey, CreatePortfolioRequest>({
      queryFn: async (request) => {
        try {
          const data = await portfoliosCreate(request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => {
        return [{ type: QueryTags.Portfolio, id: 'LIST' }]
      },
    }),
    updatePortfolio: builder.mutation<
      void,
      { request: UpdatePortfolioRequest; cacheKey: number }
    >({
      queryFn: async ({ request, cacheKey }) => {
        try {
          const data = await portfoliosUpdate(request.id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { request, cacheKey }) => {
        return [
          { type: QueryTags.Portfolio, id: 'LIST' },
          { type: QueryTags.Portfolio, id: cacheKey },
          ppmActivityTag(request.id),
        ]
      },
    }),
    activatePortfolio: builder.mutation<void, { id: string; cacheKey: number }>(
      {
        queryFn: async ({ id }) => {
          try {
            const data = await portfoliosActivate(id)
            return { data }
          } catch (error) {
            console.error('API Error:', error)
            return { error }
          }
        },
        invalidatesTags: (result, error, { id, cacheKey }) => {
          return [
            { type: QueryTags.Portfolio, id: 'LIST' },
            { type: QueryTags.Portfolio, id: cacheKey },
            ppmActivityTag(id),
          ]
        },
      },
    ),
    closePortfolio: builder.mutation<void, { id: string; cacheKey: number }>({
      queryFn: async ({ id }) => {
        try {
          const data = await portfoliosClose(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { id, cacheKey }) => {
        return [
          { type: QueryTags.Portfolio, id: 'LIST' },
          { type: QueryTags.Portfolio, id: cacheKey },
          ppmActivityTag(id),
        ]
      },
    }),
    archivePortfolio: builder.mutation<void, { id: string; cacheKey: number }>({
      queryFn: async ({ id }) => {
        try {
          const data = await portfoliosArchive(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { id, cacheKey }) => {
        return [
          { type: QueryTags.Portfolio, id: 'LIST' },
          { type: QueryTags.Portfolio, id: cacheKey },
          ppmActivityTag(id),
        ]
      },
    }),
    deletePortfolio: builder.mutation<
      void,
      { portfolioId: string; cacheKey: number }
    >({
      queryFn: async ({ portfolioId }) => {
        try {
          const data = await portfoliosDelete(portfolioId)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { cacheKey }) => {
        return [{ type: QueryTags.Portfolio, id: 'LIST' }]
      },
    }),
    assignPortfolioScoringModel: builder.mutation<
      void,
      { id: string; scoringModelId: string; cacheKey: number }
    >({
      queryFn: async ({ id, scoringModelId }) => {
        try {
          const data = await portfoliosAssignScoringModel(id, {
            scoringModelId,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { cacheKey, id }) => [
        { type: QueryTags.Portfolio, id: cacheKey },
        ppmActivityTag(id),
        { type: QueryTags.PortfolioRankingScoreboard, id: 'LIST' },
        { type: QueryTags.PortfolioRankingScoreboard, id },
      ],
    }),
    clearPortfolioScoringModel: builder.mutation<
      void,
      { id: string; cacheKey: number }
    >({
      queryFn: async ({ id }) => {
        try {
          const data = await portfoliosClearScoringModel(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { cacheKey, id }) => [
        { type: QueryTags.Portfolio, id: cacheKey },
        ppmActivityTag(id),
        { type: QueryTags.PortfolioRankingScoreboard, id: 'LIST' },
        { type: QueryTags.PortfolioRankingScoreboard, id },
      ],
    }),
    movePortfolioProjectRanks: builder.mutation<
      void,
      { id: string; request: MoveProjectRanksRequest; portfolioIdOrKey: string }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await portfoliosMoveProjectRanks(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { portfolioIdOrKey }) => [
        { type: QueryTags.PortfolioProjects, id: 'LIST' },
        { type: QueryTags.PortfolioProjects, id: portfolioIdOrKey },
      ],
    }),
    rebalancePortfolioProjectRanks: builder.mutation<
      void,
      { id: string; portfolioIdOrKey: string }
    >({
      queryFn: async ({ id }) => {
        try {
          const data = await portfoliosRebalanceProjectRanks(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { portfolioIdOrKey }) => [
        { type: QueryTags.PortfolioProjects, id: 'LIST' },
        { type: QueryTags.PortfolioProjects, id: portfolioIdOrKey },
      ],
    }),
    getPortfolioRankingScoreboard: builder.query<
      PortfolioRankingScoreboardDto,
      string
    >({
      queryFn: async (portfolioId) => {
        try {
          const data = await portfoliosGetRankingScoreboard(portfolioId)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, portfolioId) => [
        { type: QueryTags.PortfolioRankingScoreboard, id: 'LIST' },
        { type: QueryTags.PortfolioRankingScoreboard, id: portfolioId },
      ],
    }),
    getPortfolioPrograms: builder.query<
      ProgramListDto[],
      { portfolioIdOrKey: string; status?: ProgramStatus[] }
    >({
      queryFn: async ({ portfolioIdOrKey, status }) => {
        try {
          const data = await portfoliosGetPrograms(portfolioIdOrKey, {
            status: (status?.length ?? 0) > 0 ? status : undefined,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, { portfolioIdOrKey }) => [
        { type: QueryTags.PortfolioPrograms, id: 'LIST' },
        { type: QueryTags.PortfolioPrograms, id: portfolioIdOrKey },
      ],
    }),
    getPortfolioProjects: builder.query<
      ProjectListDto[],
      { portfolioIdOrKey: string; status?: ProjectStatus[] }
    >({
      queryFn: async ({ portfolioIdOrKey, status }) => {
        try {
          const data = await portfoliosGetProjects(portfolioIdOrKey, {
            status: status,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, { portfolioIdOrKey }) => [
        { type: QueryTags.PortfolioProjects, id: 'LIST' },
        { type: QueryTags.PortfolioProjects, id: portfolioIdOrKey },
      ],
    }),
    getPortfolioStrategicInitiatives: builder.query<
      StrategicInitiativeListDto[],
      { portfolioIdOrKey: string; status?: StrategicInitiativeStatus[] }
    >({
      queryFn: async ({ portfolioIdOrKey, status }) => {
        try {
          const data = await portfoliosGetStrategicInitiatives(
            portfolioIdOrKey,
            { status: (status?.length ?? 0) > 0 ? status : undefined },
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, { portfolioIdOrKey }) => [
        { type: QueryTags.PortfolioStrategicInitiatives, id: 'LIST' },
        { type: QueryTags.PortfolioStrategicInitiatives, id: portfolioIdOrKey },
      ],
    }),
    getPortfolioStatusOptions: builder.query<
      OptionModel<ProjectPortfolioStatus>[],
      void
    >({
      queryFn: async () => {
        try {
          const statuses = await portfoliosGetPortfolioStatuses()

          const data: OptionModel<ProjectPortfolioStatus>[] = statuses
            .sort((a, b) => a.order - b.order)
            .map((s) => ({
              value: s.code,
              label: s.name,
            }))

          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [QueryTags.PortfolioStatusOptions],
    }),

    getPortfolioOptions: builder.query<BaseOptionType[], void>({
      queryFn: async () => {
        try {
          const portfolios = await portfoliosGetPortfolioOptions()

          const data: BaseOptionType[] = portfolios
            .sort((a, b) => a.name.localeCompare(b.name))
            .map((category) => ({
              label: category.name,
              value: category.id,
            }))

          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
    }),
    getPortfolioProgramOptions: builder.query<BaseOptionType[], string>({
      queryFn: async (portfolioIdOrKey) => {
        try {
          // TODO: hard coding status for now.  Need status values from the client.
          const programs = await portfoliosGetPrograms(portfolioIdOrKey, {
            status: [ProgramStatus.Active],
          })

          const data: BaseOptionType[] = programs
            .sort((a, b) => a.name.localeCompare(b.name))
            .map((category) => ({
              label: category.name,
              value: category.id,
            }))

          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.PortfolioPrograms, id: 'LIST' },
        { type: QueryTags.PortfolioPrograms, id: arg },
      ],
    }),

    getPortfolioActivities: builder.query<
      PagedResponseOfActivityLogDto,
      { idOrKey: string | number; page?: number; pageSize?: number }
    >({
      queryFn: async ({ idOrKey, page, pageSize }) => {
        try {
          const data = await portfoliosGetActivities(String(idOrKey), {
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
})

export const {
  useGetPortfoliosQuery,
  useGetPortfolioQuery,
  useCreatePortfolioMutation,
  useUpdatePortfolioMutation,
  useActivatePortfolioMutation,
  useClosePortfolioMutation,
  useArchivePortfolioMutation,
  useDeletePortfolioMutation,
  useAssignPortfolioScoringModelMutation,
  useClearPortfolioScoringModelMutation,
  useMovePortfolioProjectRanksMutation,
  useRebalancePortfolioProjectRanksMutation,
  useGetPortfolioRankingScoreboardQuery,
  useGetPortfolioProgramsQuery,
  useGetPortfolioProjectsQuery,
  useGetPortfolioStrategicInitiativesQuery,
  useGetPortfolioOptionsQuery,
  useGetPortfolioProgramOptionsQuery,
  useGetPortfolioStatusOptionsQuery,
  useGetPortfolioActivitiesQuery,
  useLazyGetPortfolioActivitiesQuery,
} = portfoliosApi

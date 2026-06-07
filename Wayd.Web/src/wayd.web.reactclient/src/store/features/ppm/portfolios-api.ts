import { getPortfoliosClient } from '@/src/services/clients'
import { apiSlice } from '../apiSlice'
import {
  CreatePortfolioRequest,
  MoveProjectRanksRequest,
  ObjectIdAndKey,
  ProgramListDto,
  ProjectListDto,
  ProjectPortfolioDetailsDto,
  ProjectPortfolioListDto,
  StrategicInitiativeListDto,
  UpdatePortfolioRequest,
} from '@/src/services/wayd-api'
import { QueryTags } from '../query-tags'
import { BaseOptionType } from 'antd/es/select'
import { OptionModel } from '@/src/components/types'

export const portfoliosApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getPortfolios: builder.query<
      ProjectPortfolioListDto[],
      number[] | undefined
    >({
      queryFn: async (portfolioState = undefined) => {
        try {
          const data = await getPortfoliosClient().getPortfolios(portfolioState)
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
          const data = await getPortfoliosClient().getPortfolio(key.toString())
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
          const data = await getPortfoliosClient().create(request)
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
          const data = await getPortfoliosClient().update(request.id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { cacheKey }) => {
        return [
          { type: QueryTags.Portfolio, id: 'LIST' },
          { type: QueryTags.Portfolio, id: cacheKey },
        ]
      },
    }),
    activatePortfolio: builder.mutation<void, { id: string; cacheKey: number }>(
      {
        queryFn: async ({ id }) => {
          try {
            const data = await getPortfoliosClient().activate(id)
            return { data }
          } catch (error) {
            console.error('API Error:', error)
            return { error }
          }
        },
        invalidatesTags: (result, error, { cacheKey }) => {
          return [
            { type: QueryTags.Portfolio, id: 'LIST' },
            { type: QueryTags.Portfolio, id: cacheKey },
          ]
        },
      },
    ),
    closePortfolio: builder.mutation<void, { id: string; cacheKey: number }>({
      queryFn: async ({ id }) => {
        try {
          const data = await getPortfoliosClient().close(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { cacheKey }) => {
        return [
          { type: QueryTags.Portfolio, id: 'LIST' },
          { type: QueryTags.Portfolio, id: cacheKey },
        ]
      },
    }),
    archivePortfolio: builder.mutation<void, { id: string; cacheKey: number }>({
      queryFn: async ({ id }) => {
        try {
          const data = await getPortfoliosClient().archive(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { cacheKey }) => {
        return [
          { type: QueryTags.Portfolio, id: 'LIST' },
          { type: QueryTags.Portfolio, id: cacheKey },
        ]
      },
    }),
    deletePortfolio: builder.mutation<
      void,
      { portfolioId: string; cacheKey: number }
    >({
      queryFn: async ({ portfolioId }) => {
        try {
          const data = await getPortfoliosClient().delete(portfolioId)
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
          const data = await getPortfoliosClient().assignScoringModel(id, {
            scoringModelId,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { cacheKey }) => [
        { type: QueryTags.Portfolio, id: cacheKey },
      ],
    }),
    clearPortfolioScoringModel: builder.mutation<
      void,
      { id: string; cacheKey: number }
    >({
      queryFn: async ({ id }) => {
        try {
          const data = await getPortfoliosClient().clearScoringModel(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { cacheKey }) => [
        { type: QueryTags.Portfolio, id: cacheKey },
      ],
    }),
    movePortfolioProjectRanks: builder.mutation<
      void,
      { id: string; request: MoveProjectRanksRequest; portfolioIdOrKey: string }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await getPortfoliosClient().moveProjectRanks(id, request)
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
          const data = await getPortfoliosClient().rebalanceProjectRanks(id)
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
    getPortfolioPrograms: builder.query<
      ProgramListDto[],
      { portfolioIdOrKey: string; status?: number[] }
    >({
      queryFn: async ({ portfolioIdOrKey, status }) => {
        try {
          const data = await getPortfoliosClient().getPrograms(
            portfolioIdOrKey,
            (status?.length ?? 0) > 0 ? status : undefined,
          )
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
      { portfolioIdOrKey: string; status?: number[] }
    >({
      queryFn: async ({ portfolioIdOrKey, status }) => {
        try {
          const data = await getPortfoliosClient().getProjects(
            portfolioIdOrKey,
            status,
          )
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
      { portfolioIdOrKey: string; status?: number[] }
    >({
      queryFn: async ({ portfolioIdOrKey, status }) => {
        try {
          const data = await getPortfoliosClient().getStrategicInitiatives(
            portfolioIdOrKey,
            (status?.length ?? 0) > 0 ? status : undefined,
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
    getPortfolioStatusOptions: builder.query<OptionModel<number>[], void>({
      queryFn: async () => {
        try {
          const statuses = await getPortfoliosClient().getPortfolioStatuses()

          const data: OptionModel<number>[] = statuses
            .sort((a, b) => a.order - b.order)
            .map((s) => ({
              value: s.id,
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
          const portfolios = await getPortfoliosClient().getPortfolioOptions()

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
          const programs = await getPortfoliosClient().getPrograms(
            portfolioIdOrKey,
            [2],
          )

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
  useGetPortfolioProgramsQuery,
  useGetPortfolioProjectsQuery,
  useGetPortfolioStrategicInitiativesQuery,
  useGetPortfolioOptionsQuery,
  useGetPortfolioProgramOptionsQuery,
  useGetPortfolioStatusOptionsQuery,
} = portfoliosApi

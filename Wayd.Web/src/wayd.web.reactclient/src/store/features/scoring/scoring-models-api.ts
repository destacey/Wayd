import { getScoringModelsClient } from '@/src/services/clients'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import {
  CreateScoringModelRequest,
  ScoringModelCriterionRequest,
  ScoringModelDetailsDto,
  ScoringModelListDto,
  ScoringModelOutputRequest,
  ScoringModelState,
  ScoringScaleLevelRequest,
  ScoringScaleRequest,
  UpdateScoringModelRequest,
} from '@/src/services/wayd-api'

export const scoringModelsApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getScoringModels: builder.query<
      ScoringModelListDto[],
      ScoringModelState | null | undefined
    >({
      queryFn: async (state) => {
        try {
          const data = await getScoringModelsClient().getScoringModels(state)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [{ type: QueryTags.ScoringModel, id: 'LIST' }],
    }),
    getScoringModel: builder.query<ScoringModelDetailsDto, string>({
      queryFn: async (idOrKey) => {
        try {
          const data = await getScoringModelsClient().getScoringModel(idOrKey)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.ScoringModel, id: arg },
      ],
    }),
    createScoringModel: builder.mutation<string, CreateScoringModelRequest>({
      queryFn: async (request) => {
        try {
          const data = await getScoringModelsClient().create(request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [{ type: QueryTags.ScoringModel, id: 'LIST' }],
    }),
    updateScoringModel: builder.mutation<
      void,
      { id: string } & UpdateScoringModelRequest
    >({
      queryFn: async (request) => {
        try {
          const data = await getScoringModelsClient().update(request.id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.ScoringModel, id: 'LIST' },
        { type: QueryTags.ScoringModel, id: arg.id },
      ],
    }),
    deleteScoringModel: builder.mutation<void, string>({
      queryFn: async (id) => {
        try {
          const data = await getScoringModelsClient().delete(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [{ type: QueryTags.ScoringModel, id: 'LIST' }],
    }),
    activateScoringModel: builder.mutation<void, string>({
      queryFn: async (id) => {
        try {
          const data = await getScoringModelsClient().activate(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.ScoringModel, id: 'LIST' },
        { type: QueryTags.ScoringModel, id: arg },
      ],
    }),
    archiveScoringModel: builder.mutation<void, string>({
      queryFn: async (id) => {
        try {
          const data = await getScoringModelsClient().archive(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.ScoringModel, id: 'LIST' },
        { type: QueryTags.ScoringModel, id: arg },
      ],
    }),
    addScoringModelCriterion: builder.mutation<
      string,
      { scoringModelId: string } & ScoringModelCriterionRequest
    >({
      queryFn: async ({ scoringModelId, ...request }) => {
        try {
          const data = await getScoringModelsClient().addCriterion(
            scoringModelId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [{ type: QueryTags.ScoringModel }],
    }),
    updateScoringModelCriterion: builder.mutation<
      void,
      { scoringModelId: string; criterionId: string } & ScoringModelCriterionRequest
    >({
      queryFn: async ({ scoringModelId, criterionId, ...request }) => {
        try {
          const data = await getScoringModelsClient().updateCriterion(
            scoringModelId,
            criterionId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [{ type: QueryTags.ScoringModel }],
    }),
    removeScoringModelCriterion: builder.mutation<
      void,
      { scoringModelId: string; criterionId: string }
    >({
      queryFn: async ({ scoringModelId, criterionId }) => {
        try {
          const data = await getScoringModelsClient().removeCriterion(
            scoringModelId,
            criterionId,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [{ type: QueryTags.ScoringModel }],
    }),
    reorderScoringModelCriteria: builder.mutation<
      void,
      { scoringModelId: string; orderedCriterionIds: string[] }
    >({
      queryFn: async ({ scoringModelId, orderedCriterionIds }) => {
        try {
          const data = await getScoringModelsClient().reorderCriteria(
            scoringModelId,
            { orderedCriterionIds },
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [{ type: QueryTags.ScoringModel }],
    }),
    addScoringScale: builder.mutation<
      string,
      { scoringModelId: string } & ScoringScaleRequest
    >({
      queryFn: async ({ scoringModelId, ...request }) => {
        try {
          const data = await getScoringModelsClient().addScale(
            scoringModelId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [{ type: QueryTags.ScoringModel }],
    }),
    updateScoringScale: builder.mutation<
      void,
      { scoringModelId: string; scaleId: string } & ScoringScaleRequest
    >({
      queryFn: async ({ scoringModelId, scaleId, ...request }) => {
        try {
          const data = await getScoringModelsClient().updateScale(
            scoringModelId,
            scaleId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [{ type: QueryTags.ScoringModel }],
    }),
    removeScoringScale: builder.mutation<
      void,
      { scoringModelId: string; scaleId: string }
    >({
      queryFn: async ({ scoringModelId, scaleId }) => {
        try {
          const data = await getScoringModelsClient().removeScale(
            scoringModelId,
            scaleId,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [{ type: QueryTags.ScoringModel }],
    }),
    reorderScoringScales: builder.mutation<
      void,
      { scoringModelId: string; orderedScaleIds: string[] }
    >({
      queryFn: async ({ scoringModelId, orderedScaleIds }) => {
        try {
          const data = await getScoringModelsClient().reorderScales(
            scoringModelId,
            { orderedScaleIds },
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [{ type: QueryTags.ScoringModel }],
    }),
    addScoringScaleLevel: builder.mutation<
      string,
      { scoringModelId: string; scaleId: string } & ScoringScaleLevelRequest
    >({
      queryFn: async ({ scoringModelId, scaleId, ...request }) => {
        try {
          const data = await getScoringModelsClient().addScaleLevel(
            scoringModelId,
            scaleId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [{ type: QueryTags.ScoringModel }],
    }),
    updateScoringScaleLevel: builder.mutation<
      void,
      {
        scoringModelId: string
        scaleId: string
        levelId: string
      } & ScoringScaleLevelRequest
    >({
      queryFn: async ({ scoringModelId, scaleId, levelId, ...request }) => {
        try {
          const data = await getScoringModelsClient().updateScaleLevel(
            scoringModelId,
            scaleId,
            levelId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [{ type: QueryTags.ScoringModel }],
    }),
    removeScoringScaleLevel: builder.mutation<
      void,
      { scoringModelId: string; scaleId: string; levelId: string }
    >({
      queryFn: async ({ scoringModelId, scaleId, levelId }) => {
        try {
          const data = await getScoringModelsClient().removeScaleLevel(
            scoringModelId,
            scaleId,
            levelId,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [{ type: QueryTags.ScoringModel }],
    }),
    reorderScoringScaleLevels: builder.mutation<
      void,
      { scoringModelId: string; scaleId: string; orderedLevelIds: string[] }
    >({
      queryFn: async ({ scoringModelId, scaleId, orderedLevelIds }) => {
        try {
          const data = await getScoringModelsClient().reorderScaleLevels(
            scoringModelId,
            scaleId,
            { orderedLevelIds },
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [{ type: QueryTags.ScoringModel }],
    }),
    addScoringModelOutput: builder.mutation<
      string,
      { scoringModelId: string } & ScoringModelOutputRequest
    >({
      queryFn: async ({ scoringModelId, ...request }) => {
        try {
          const data = await getScoringModelsClient().addOutput(
            scoringModelId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [{ type: QueryTags.ScoringModel }],
    }),
    updateScoringModelOutput: builder.mutation<
      void,
      { scoringModelId: string; outputId: string } & ScoringModelOutputRequest
    >({
      queryFn: async ({ scoringModelId, outputId, ...request }) => {
        try {
          const data = await getScoringModelsClient().updateOutput(
            scoringModelId,
            outputId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [{ type: QueryTags.ScoringModel }],
    }),
    removeScoringModelOutput: builder.mutation<
      void,
      { scoringModelId: string; outputId: string }
    >({
      queryFn: async ({ scoringModelId, outputId }) => {
        try {
          const data = await getScoringModelsClient().removeOutput(
            scoringModelId,
            outputId,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [{ type: QueryTags.ScoringModel }],
    }),
    reorderScoringModelOutputs: builder.mutation<
      void,
      { scoringModelId: string; orderedOutputIds: string[] }
    >({
      queryFn: async ({ scoringModelId, orderedOutputIds }) => {
        try {
          const data = await getScoringModelsClient().reorderOutputs(
            scoringModelId,
            { orderedOutputIds },
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [{ type: QueryTags.ScoringModel }],
    }),
  }),
})

export const {
  useGetScoringModelsQuery,
  useGetScoringModelQuery,
  useCreateScoringModelMutation,
  useUpdateScoringModelMutation,
  useDeleteScoringModelMutation,
  useActivateScoringModelMutation,
  useArchiveScoringModelMutation,
  useAddScoringModelCriterionMutation,
  useUpdateScoringModelCriterionMutation,
  useRemoveScoringModelCriterionMutation,
  useReorderScoringModelCriteriaMutation,
  useAddScoringScaleMutation,
  useUpdateScoringScaleMutation,
  useRemoveScoringScaleMutation,
  useReorderScoringScalesMutation,
  useAddScoringScaleLevelMutation,
  useUpdateScoringScaleLevelMutation,
  useRemoveScoringScaleLevelMutation,
  useReorderScoringScaleLevelsMutation,
  useAddScoringModelOutputMutation,
  useUpdateScoringModelOutputMutation,
  useRemoveScoringModelOutputMutation,
  useReorderScoringModelOutputsMutation,
} = scoringModelsApi

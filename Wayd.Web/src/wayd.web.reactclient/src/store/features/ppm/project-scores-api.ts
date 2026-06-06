import { getProjectScoresClient } from '@/src/services/clients'
import { apiSlice } from '../apiSlice'
import {
  ProjectScoreDetailsDto,
  ProjectScoreSummaryDto,
  ProjectScoringContextDto,
  RecordProjectScoreRequest,
} from '@/src/services/wayd-api'
import { QueryTags } from '../query-tags'

export interface ProjectScoreScope {
  projectId: string
}

export interface ProjectScoreRef extends ProjectScoreScope {
  scoreId: string
}

export interface RecordProjectScoreArgs extends ProjectScoreScope {
  request: RecordProjectScoreRequest
}

export const projectScoresApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getProjectScoringContext: builder.query<
      ProjectScoringContextDto,
      ProjectScoreScope
    >({
      queryFn: async ({ projectId }) => {
        try {
          const data = await getProjectScoresClient().getScoringContext(projectId)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (_result, _error, { projectId }) => [
        { type: QueryTags.ProjectScoringContext, id: projectId },
      ],
    }),

    getProjectScores: builder.query<ProjectScoreSummaryDto[], ProjectScoreScope>({
      queryFn: async ({ projectId }) => {
        try {
          const data = await getProjectScoresClient().getScores(projectId)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result = [], _error, { projectId }) => [
        { type: QueryTags.ProjectScore, id: `LIST-${projectId}` },
        ...result.map(({ id }) => ({ type: QueryTags.ProjectScore, id })),
      ],
    }),

    getProjectScore: builder.query<ProjectScoreDetailsDto, ProjectScoreRef>({
      queryFn: async ({ projectId, scoreId }) => {
        try {
          const data = await getProjectScoresClient().getScore(projectId, scoreId)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (_result, _error, { scoreId }) => [
        { type: QueryTags.ProjectScore, id: scoreId },
      ],
    }),

    recordProjectScore: builder.mutation<string, RecordProjectScoreArgs>({
      queryFn: async ({ projectId, request }) => {
        try {
          const data = await getProjectScoresClient().recordScore(projectId, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (_result, _error, { projectId }) => [
        { type: QueryTags.ProjectScore, id: `LIST-${projectId}` },
        { type: QueryTags.ProjectScoringContext, id: projectId },
        { type: QueryTags.Project, id: 'LIST' },
        { type: QueryTags.Project, id: projectId },
      ],
    }),
  }),
})

export const {
  useGetProjectScoringContextQuery,
  useGetProjectScoresQuery,
  useGetProjectScoreQuery,
  useRecordProjectScoreMutation,
} = projectScoresApi

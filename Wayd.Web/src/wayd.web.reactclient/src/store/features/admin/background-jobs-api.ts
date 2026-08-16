import {
  BackgroundJobDto,
  BackgroundJobTypeDto,
  CreateRecurringJobRequest,
  JobDetailResponse,
  JobServerResponse,
  JobStateFilter,
  JobStatisticsResponse,
  JobsResponse,
  RecurringJobResponse,
} from '@/src/services/wayd-api'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import { getBackgroundJobsClient } from '@/src/services/clients'

export interface GetJobsRequest {
  state: JobStateFilter
  /** 0-based. */
  pageNumber?: number
  pageSize?: number
}

/** Every list/tile view reflects a mutation, so job actions invalidate all of them. */
const jobViewTags = [
  QueryTags.BackgroundJob,
  QueryTags.BackgroundJobStatistics,
]

/**
 * Jobs move without the user doing anything, so the page polls while it is open.
 * RTK Query only polls for mounted components and stops on unmount, so these
 * cost nothing once the user navigates away.
 */
export const STATISTICS_POLLING_MS = 5000
export const JOB_LIST_POLLING_MS = 10000
/** Schedules only move when a cron fires, so Next Run / Last Result drift slowly. */
export const RECURRING_JOB_POLLING_MS = 30000
/** Heartbeats are for spotting a dead worker, which a minute of lag still catches. */
export const JOB_SERVER_POLLING_MS = 60000

export const backgroundJobsApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getRunningJobs: builder.query<BackgroundJobDto[], void>({
      queryFn: async () => {
        try {
          const data = await getBackgroundJobsClient().getRunningJobs()
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result) => [
        QueryTags.BackgroundJob,
        ...(result?.map(({ id }) => ({ type: QueryTags.BackgroundJob, id })) ??
          []),
      ],
    }),
    getJobs: builder.query<JobsResponse, GetJobsRequest>({
      queryFn: async (request) => {
        try {
          const data = await getBackgroundJobsClient().getJobs(
            request.state,
            request.pageNumber,
            request.pageSize,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result) => [
        QueryTags.BackgroundJob,
        ...(result?.items?.map(({ id }) => ({
          type: QueryTags.BackgroundJob,
          id,
        })) ?? []),
      ],
    }),
    getJobDetail: builder.query<JobDetailResponse, string>({
      queryFn: async (jobId) => {
        try {
          const data = await getBackgroundJobsClient().getJobDetail(jobId)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, jobId) => [
        { type: QueryTags.BackgroundJob, id: jobId },
      ],
    }),
    getJobStatistics: builder.query<JobStatisticsResponse, void>({
      queryFn: async () => {
        try {
          const data = await getBackgroundJobsClient().getStatistics()
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: [QueryTags.BackgroundJobStatistics],
    }),
    getJobServers: builder.query<JobServerResponse[], void>({
      queryFn: async () => {
        try {
          const data = await getBackgroundJobsClient().getServers()
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: [QueryTags.BackgroundJobServer],
    }),
    getRecurringJobs: builder.query<RecurringJobResponse[], void>({
      queryFn: async () => {
        try {
          const data = await getBackgroundJobsClient().getRecurringJobs()
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result) => [
        QueryTags.RecurringJob,
        ...(result?.map(({ id }) => ({ type: QueryTags.RecurringJob, id })) ??
          []),
      ],
    }),
    getJobTypes: builder.query<BackgroundJobTypeDto[], void>({
      queryFn: async () => {
        try {
          const data = await getBackgroundJobsClient().getJobTypes()
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result) => [
        QueryTags.BackgroundJobType,
        ...(result?.map(({ id }) => ({
          type: QueryTags.BackgroundJobType,
          id,
        })) ?? []),
      ],
    }),
    runJob: builder.mutation<void, number>({
      queryFn: async (jobTypeId) => {
        try {
          const data = await getBackgroundJobsClient().run(jobTypeId)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: jobViewTags,
    }),
    requeueJob: builder.mutation<void, string>({
      queryFn: async (jobId) => {
        try {
          const data = await getBackgroundJobsClient().requeueJob(jobId)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: jobViewTags,
    }),
    deleteJob: builder.mutation<void, string>({
      queryFn: async (jobId) => {
        try {
          const data = await getBackgroundJobsClient().deleteJob(jobId)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: jobViewTags,
    }),
    createRecurringJob: builder.mutation<void, CreateRecurringJobRequest>({
      queryFn: async (request) => {
        try {
          const data = await getBackgroundJobsClient().create(request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: [QueryTags.RecurringJob, ...jobViewTags],
    }),
    removeRecurringJob: builder.mutation<void, string>({
      queryFn: async (recurringJobId) => {
        try {
          const data =
            await getBackgroundJobsClient().removeRecurringJob(recurringJobId)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: [QueryTags.RecurringJob, ...jobViewTags],
    }),
  }),
})

export const {
  useGetRunningJobsQuery,
  useGetJobsQuery,
  useGetJobDetailQuery,
  useGetJobStatisticsQuery,
  useGetJobServersQuery,
  useGetRecurringJobsQuery,
  useGetJobTypesQuery,
  useRunJobMutation,
  useRequeueJobMutation,
  useDeleteJobMutation,
  useCreateRecurringJobMutation,
  useRemoveRecurringJobMutation,
} = backgroundJobsApi

import {
  BackgroundJobDto,
  BackgroundJobTypeDto,
  CreateRecurringJobRequest,
  JobDetailResponse,
  JobServerResponse,
  JobStateFilter,
  JobStatisticsResponse,
  PagedResponseOfJobSummaryResponse,
  RecurringJobResponse,
} from '@/src/services/wayd-api'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import { getBackgroundJobsClient } from '@/src/services/clients'
import { chunk } from '@/src/utils/chunk'

export interface GetJobsRequest {
  state: JobStateFilter
  /** 1-based. */
  page?: number
  pageSize?: number
}

/** Every list/tile view reflects a mutation, so job actions invalidate all of them. */
const jobViewTags = [QueryTags.BackgroundJob, QueryTags.BackgroundJobStatistics]

/** How a bulk job action went: how many jobs it covered and how many of them it could not act on. */
export interface BulkJobOutcome {
  total: number
  failed: number
}

/** Requests a bulk action keeps in flight at once, so a 500-job page doesn't fire 500 at the API together. */
export const BULK_JOB_CONCURRENCY = 10

/**
 * Runs `action` for every job id, {@link BULK_JOB_CONCURRENCY} at a time, and
 * counts the failures rather than stopping at the first one: each job stands
 * alone, so one that changed state must not block the rest.
 */
export const settleJobsInBatches = async (
  jobIds: string[],
  action: (jobId: string) => Promise<unknown>,
): Promise<BulkJobOutcome> => {
  let failed = 0
  for (const slice of chunk(jobIds, BULK_JOB_CONCURRENCY)) {
    const results = await Promise.allSettled(slice.map(action))
    failed += results.filter((r) => r.status === 'rejected').length
  }
  return { total: jobIds.length, failed }
}

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
    getJobs: builder.query<PagedResponseOfJobSummaryResponse, GetJobsRequest>({
      queryFn: async (request) => {
        try {
          const data = await getBackgroundJobsClient().getJobs(
            request.state,
            request.page,
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
    // The bulk mutations call the client directly, so the lists refetch once
    // for the batch rather than once per job.
    requeueJobs: builder.mutation<BulkJobOutcome, string[]>({
      queryFn: async (jobIds) => ({
        data: await settleJobsInBatches(jobIds, (jobId) =>
          getBackgroundJobsClient().requeueJob(jobId),
        ),
      }),
      invalidatesTags: jobViewTags,
    }),
    deleteJobs: builder.mutation<BulkJobOutcome, string[]>({
      queryFn: async (jobIds) => ({
        data: await settleJobsInBatches(jobIds, (jobId) =>
          getBackgroundJobsClient().deleteJob(jobId),
        ),
      }),
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
  useRequeueJobsMutation,
  useDeleteJobsMutation,
  useCreateRecurringJobMutation,
  useRemoveRecurringJobMutation,
} = backgroundJobsApi

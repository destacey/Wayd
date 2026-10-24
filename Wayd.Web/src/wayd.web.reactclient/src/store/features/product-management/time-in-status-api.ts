import {
  getDeploymentsClient,
  getProductsClient,
  getReleasePackagesClient,
  getReleasesClient,
  getVersionsClient,
} from '@/src/services/clients'
import { TimeInStatusReportDto } from '@/src/services/wayd-api'
import { apiSlice } from '../apiSlice'

/** The status-tracked record types the time in status report covers. */
export type TimeInStatusRecordType =
  'products' | 'versions' | 'releases' | 'releasePackages' | 'deployments'

/**
 * ISO-8601 strings rather than `Date`s: query arguments become the cache key in the Redux store, where
 * a `Date` is non-serializable. The client wants `Date`s, so the queryFn converts.
 */
export interface GetTimeInStatusRequest {
  recordType: TimeInStatusRecordType
  from: string
  to: string
}

const fetchReport = (
  recordType: TimeInStatusRecordType,
  from: Date,
  to: Date,
): Promise<TimeInStatusReportDto> => {
  switch (recordType) {
    case 'products':
      return getProductsClient().getTimeInStatus(from, to)
    case 'versions':
      return getVersionsClient().getTimeInStatus(from, to)
    case 'releases':
      return getReleasesClient().getTimeInStatus(from, to)
    case 'releasePackages':
      return getReleasePackagesClient().getTimeInStatus(from, to)
    case 'deployments':
      return getDeploymentsClient().getTimeInStatus(from, to)
  }
}

export const timeInStatusApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    /**
     * How long records of one type stayed in each status, for stays that ended in the window.
     *
     * Untagged: any status change on any record of the type can move the figures, and no mutation knows
     * which cached windows that touches. The cache expires on its own once the report is closed.
     */
    getTimeInStatus: builder.query<
      TimeInStatusReportDto,
      GetTimeInStatusRequest
    >({
      queryFn: async (request) => {
        try {
          const data = await fetchReport(
            request.recordType,
            new Date(request.from),
            new Date(request.to),
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
    }),
  }),
})

export const { useGetTimeInStatusQuery } = timeInStatusApi

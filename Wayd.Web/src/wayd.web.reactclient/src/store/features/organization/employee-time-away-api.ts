import { getEmployeesClient } from '@/src/services/clients'
import {
  ChangeEmployeeHolidayCalendarRequest,
  ChangeEmployeeWorkingDayHoursRequest,
  EmployeeTimeAwayDto,
  EmployeeTimeAwayEntryDto,
  EmployeeTimeAwayRequest,
  TimeAwayReasonDto,
} from '@/src/services/wayd-api'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'

export type { EmployeeTimeAwayDto, EmployeeTimeAwayEntryDto }

// Time away, a person's holidays and their working day change every team they
// are allocated to, and the sprint ideal line is weighed by who is there, so
// each change refetches every team's capacity and every sprint's burn and scope.
const invalidatedBy = (employeeId: string) => [
  { type: QueryTags.EmployeeTimeAway, id: employeeId },
  QueryTags.TeamCapacity,
  QueryTags.SprintBurn,
  QueryTags.SprintScope,
]

export const employeeTimeAwayApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getEmployeeTimeAway: builder.query<
      EmployeeTimeAwayDto,
      { employeeId: string }
    >({
      queryFn: async ({ employeeId }) => {
        try {
          const data = await getEmployeesClient().getTimeAway(employeeId)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.EmployeeTimeAway, id: arg.employeeId },
      ],
    }),

    getTimeAwayReasons: builder.query<TimeAwayReasonDto[], void>({
      queryFn: async () => {
        try {
          const data = await getEmployeesClient().getTimeAwayReasons()
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      // The list only changes with a server release.
      keepUnusedDataFor: 60 * 60,
    }),

    recordEmployeeTimeAway: builder.mutation<
      string,
      { employeeId: string; request: EmployeeTimeAwayRequest }
    >({
      queryFn: async ({ employeeId, request }) => {
        try {
          const data = await getEmployeesClient().recordTimeAway(
            employeeId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => invalidatedBy(arg.employeeId),
    }),

    changeEmployeeTimeAway: builder.mutation<
      void,
      {
        employeeId: string
        timeAwayId: string
        request: EmployeeTimeAwayRequest
      }
    >({
      queryFn: async ({ employeeId, timeAwayId, request }) => {
        try {
          const data = await getEmployeesClient().changeTimeAway(
            employeeId,
            timeAwayId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => invalidatedBy(arg.employeeId),
    }),

    removeEmployeeTimeAway: builder.mutation<
      void,
      { employeeId: string; timeAwayId: string }
    >({
      queryFn: async ({ employeeId, timeAwayId }) => {
        try {
          const data = await getEmployeesClient().removeTimeAway(
            employeeId,
            timeAwayId,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => invalidatedBy(arg.employeeId),
    }),

    changeEmployeeHolidayCalendar: builder.mutation<
      void,
      { employeeId: string; request: ChangeEmployeeHolidayCalendarRequest }
    >({
      queryFn: async ({ employeeId, request }) => {
        try {
          const data = await getEmployeesClient().changeHolidayCalendar(
            employeeId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      // A calendar's detail page counts the employees who take it.
      invalidatesTags: (result, error, arg) => [
        ...invalidatedBy(arg.employeeId),
        QueryTags.HolidayCalendar,
      ],
    }),

    changeEmployeeWorkingDayHours: builder.mutation<
      void,
      { employeeId: string; request: ChangeEmployeeWorkingDayHoursRequest }
    >({
      queryFn: async ({ employeeId, request }) => {
        try {
          const data = await getEmployeesClient().changeWorkingDayHours(
            employeeId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => invalidatedBy(arg.employeeId),
    }),
  }),
})

export const {
  useGetEmployeeTimeAwayQuery,
  useGetTimeAwayReasonsQuery,
  useRecordEmployeeTimeAwayMutation,
  useChangeEmployeeTimeAwayMutation,
  useRemoveEmployeeTimeAwayMutation,
  useChangeEmployeeHolidayCalendarMutation,
  useChangeEmployeeWorkingDayHoursMutation,
} = employeeTimeAwayApi

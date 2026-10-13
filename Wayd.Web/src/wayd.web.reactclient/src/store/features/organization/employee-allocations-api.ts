import { getEmployeesClient } from '@/src/services/clients'
import {
  ChangeEmployeeAllocationsRequest,
  EmployeeAllocationDto,
  MoveEmployeeAllocationRequest,
} from '@/src/services/wayd-api'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'

export type { EmployeeAllocationDto }

export const employeeAllocationsApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getEmployeeAllocations: builder.query<
      EmployeeAllocationDto[],
      { employeeId: string }
    >({
      queryFn: async ({ employeeId }) => {
        try {
          const data = await getEmployeesClient().getAllocations(employeeId)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.EmployeeAllocation, id: arg.employeeId },
      ],
    }),

    changeEmployeeAllocations: builder.mutation<
      void,
      { employeeId: string; request: ChangeEmployeeAllocationsRequest }
    >({
      queryFn: async ({ employeeId, request }) => {
        try {
          const data = await getEmployeesClient().changeAllocations(
            employeeId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      // A team's capacity is made of its people's allocations, and one change
      // can touch several teams, so every team's capacity is refetched.
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.EmployeeAllocation, id: arg.employeeId },
        QueryTags.TeamCapacity,
      ],
    }),

    moveEmployeeAllocation: builder.mutation<
      void,
      { employeeId: string; request: MoveEmployeeAllocationRequest }
    >({
      queryFn: async ({ employeeId, request }) => {
        try {
          const data = await getEmployeesClient().moveAllocation(
            employeeId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      // A team's capacity is made of its people's allocations, and one change
      // can touch several teams, so every team's capacity is refetched.
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.EmployeeAllocation, id: arg.employeeId },
        QueryTags.TeamCapacity,
      ],
    }),
  }),
})

export const {
  useGetEmployeeAllocationsQuery,
  useChangeEmployeeAllocationsMutation,
  useMoveEmployeeAllocationMutation,
} = employeeAllocationsApi

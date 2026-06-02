import { BaseOptionType } from 'antd/es/select'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import { getEmployeesClient } from '@/src/services/clients'
import {
  EmployeeDetailsDto,
  EmployeeListDto,
  WorkItemListDto,
  WorkStatusCategory,
} from '@/src/services/wayd-api'

export const employeeApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getEmployees: builder.query<EmployeeListDto[], boolean | undefined>({
      queryFn: async (includeInactive) => {
        try {
          const employees = await getEmployeesClient().getList(includeInactive)
          const data = employees.sort((a, b) =>
            a.displayName.localeCompare(b.displayName),
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [
        QueryTags.Employee,
        { type: QueryTags.Employee, id: 'LIST' },
      ],
    }),
    getEmployee: builder.query<EmployeeDetailsDto, number>({
      queryFn: async (key) => {
        try {
          const data = await getEmployeesClient().getEmployee(key.toString())
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.Employee, id: arg },
      ],
    }),
    getEmployeeWorkItems: builder.query<
      WorkItemListDto[],
      {
        employeeId: string
        statusCategories?: WorkStatusCategory[] | null
        /** ISO 8601 date string (e.g., "2025-08-25T00:00:00.000Z") */
        doneFrom?: string | null
        /** ISO 8601 date string (e.g., "2025-08-25T00:00:00.000Z") */
        doneTo?: string | null
      }
    >({
      queryFn: async ({ employeeId, statusCategories, doneFrom, doneTo }) => {
        try {
          const data = await getEmployeesClient().getEmployeeWorkItems(
            employeeId,
            statusCategories,
            doneFrom ? new Date(doneFrom) : null,
            doneTo ? new Date(doneTo) : null,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        QueryTags.EmployeeWorkItems,
        {
          type: QueryTags.EmployeeWorkItems,
          id: `${arg.employeeId}-${arg.statusCategories?.join(',') ?? ''}-${arg.doneFrom ?? ''}-${arg.doneTo ?? ''}`,
        },
      ],
    }),
    getEmployeeOptions: builder.query<BaseOptionType[], boolean | undefined>({
      queryFn: async (includeInactive) => {
        try {
          const employees = await getEmployeesClient().getList(includeInactive)
          const data: BaseOptionType[] = employees
            .sort((a, b) => a.displayName.localeCompare(b.displayName))
            .map((employee) => ({
              label: employee.isActive
                ? employee.displayName
                : `${employee.displayName} (Inactive)`,
              value: employee.id,
            }))
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [QueryTags.EmployeeOption],
    }),
    deleteEmployee: builder.mutation<void, string>({
      queryFn: async (employeeId) => {
        try {
          const data = await getEmployeesClient().delete(employeeId)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: [{ type: QueryTags.Employee, id: 'LIST' }],
    }),
  }),
})

export const {
  useGetEmployeesQuery,
  useGetEmployeeQuery,
  useGetEmployeeWorkItemsQuery,
  useGetEmployeeOptionsQuery,
  useDeleteEmployeeMutation,
} = employeeApi

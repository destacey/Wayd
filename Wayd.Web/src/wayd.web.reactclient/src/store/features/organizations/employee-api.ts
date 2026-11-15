import type { BaseOptionType } from 'antd/es/select'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import {
  employeesDelete,
  employeesGetDirectReports,
  employeesGetEmployee,
  employeesGetEmployeeWorkItems,
  employeesGetList,
} from '@/src/services/api/employees'
import {
  EmployeeDetailsDto,
  EmployeeListDto,
  WorkItemListDto,
  StatusCategory,
} from '@/src/services/api/wayd.schemas'

export const employeeApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getEmployees: builder.query<EmployeeListDto[], boolean | undefined>({
      queryFn: async (includeInactive) => {
        try {
          const employees = await employeesGetList({
            includeInactive: includeInactive,
          })
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
          const data = await employeesGetEmployee(key.toString())
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
    // The endpoint takes an id or a key; this is the id-keyed sibling of
    // getEmployee, for callers holding a Guid — SignalR presence and the
    // navigation DTOs carry ids rather than keys.
    getEmployeeById: builder.query<EmployeeDetailsDto, string>({
      queryFn: async (id) => {
        try {
          const data = await employeesGetEmployee(id)
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
    getDirectReports: builder.query<EmployeeListDto[], string>({
      queryFn: async (employeeId) => {
        try {
          const data = await employeesGetDirectReports(employeeId)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      // Tagged against the manager's own record: a reporting-line change
      // invalidates the employee it was made on, which refreshes this list.
      providesTags: (result, error, arg) => [
        { type: QueryTags.Employee, id: `${arg}-direct-reports` },
      ],
    }),
    getEmployeeWorkItems: builder.query<
      WorkItemListDto[],
      {
        employeeId: string
        statusCategories?: StatusCategory[] | null
        /** ISO 8601 date string (e.g., "2025-08-25T00:00:00.000Z") */
        doneFrom?: string | null
        /** ISO 8601 date string (e.g., "2025-08-25T00:00:00.000Z") */
        doneTo?: string | null
      }
    >({
      queryFn: async ({ employeeId, statusCategories, doneFrom, doneTo }) => {
        try {
          const data = await employeesGetEmployeeWorkItems(employeeId, {
            statusCategories: statusCategories ?? undefined,
            doneFrom: doneFrom ? new Date(doneFrom) : undefined,
            doneTo: doneTo ? new Date(doneTo) : undefined,
          })
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
          const employees = await employeesGetList({
            includeInactive: includeInactive,
          })
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
          const data = await employeesDelete(employeeId)
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
  useGetEmployeeByIdQuery,
  useGetDirectReportsQuery,
  useGetEmployeeWorkItemsQuery,
  useGetEmployeeOptionsQuery,
  useDeleteEmployeeMutation,
} = employeeApi

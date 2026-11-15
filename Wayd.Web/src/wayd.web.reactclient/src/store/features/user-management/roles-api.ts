import { apiSlice } from '../apiSlice'
import {
  CreateOrUpdateRoleRequest,
  RoleDto,
  RoleListDto,
  UpdateRolePermissionsRequest,
  UserDetailsDto,
} from '@/src/services/api/wayd.schemas'
import {
  rolesCreateOrUpdate,
  rolesDelete,
  rolesGetByIdWithPermissions,
  rolesGetList,
  rolesGetUsers,
  rolesGetUsersCount,
  rolesUpdatePermissions,
} from '@/src/services/api/roles'
import { QueryTags } from '../query-tags'

export const rolesApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getRoles: builder.query<RoleListDto[], void>({
      queryFn: async () => {
        try {
          const data = await rolesGetList()
          return {
            data: data.sort((a, b) => a.name.localeCompare(b.name)),
          }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result = []) => [
        { type: QueryTags.Role, id: 'LIST' },
        ...result.map(({ id }) => ({ type: QueryTags.Role, id })),
      ],
    }),

    getRole: builder.query<RoleDto, string>({
      queryFn: async (id: string) => {
        try {
          const data = await rolesGetByIdWithPermissions(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, id) => [{ type: QueryTags.Role, id }],
    }),

    upsertRole: builder.mutation<string, CreateOrUpdateRoleRequest>({
      queryFn: async (request) => {
        try {
          const data = await rolesCreateOrUpdate(request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { id }) => [
        { type: QueryTags.Role, id: 'LIST' },
        { type: QueryTags.Role, id: id ?? 'NEW' },
      ],
    }),

    updatePermissions: builder.mutation<void, UpdateRolePermissionsRequest>({
      queryFn: async (request) => {
        try {
          const data = await rolesUpdatePermissions(request.roleId, {
            roleId: request.roleId,
            permissions: request.permissions,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { roleId }) => [
        { type: QueryTags.Role, id: roleId },
      ],
    }),

    getRoleUsers: builder.query<UserDetailsDto[], string>({
      queryFn: async (id: string) => {
        try {
          const data = await rolesGetUsers(id)
          data.sort((a, b) =>
            (a.userName ?? '').localeCompare(b.userName ?? ''),
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.RoleUsers, id: arg },
      ],
    }),

    getRoleUsersCount: builder.query<number, string>({
      queryFn: async (id: string) => {
        try {
          const data = await rolesGetUsersCount(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.RoleUsersCount, id: arg },
      ],
    }),

    deleteRole: builder.mutation<void, string>({
      queryFn: async (id: string) => {
        try {
          const data = await rolesDelete(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, id) => [
        { type: QueryTags.Role, id: 'LIST' },
      ],
    }),
  }),
})

// Export hooks
export const {
  useGetRolesQuery,
  useGetRoleQuery,
  useUpsertRoleMutation,
  useUpdatePermissionsMutation,
  useGetRoleUsersQuery,
  useGetRoleUsersCountQuery,
  useDeleteRoleMutation,
} = rolesApi

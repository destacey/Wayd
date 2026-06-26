import { apiSlice } from '../apiSlice'
import {
  AssignUserRolesRequest,
  ConvertToLocalAccountRequest,
  CreateUserRequest,
  ManageRoleUsersRequest,
  StageProviderMigrationRequest,
  UserDetailsDto,
  UserIdentityDto,
  UserRoleDto,
} from '@/src/services/wayd-api'
import { authenticatedFetch, getUsersClient } from '@/src/services/clients'
import { QueryTags } from '../query-tags'
import { BaseOptionType } from 'antd/es/select'

export const usersApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getUsers: builder.query<UserDetailsDto[], void>({
      queryFn: async () => {
        try {
          const data = await getUsersClient().getUsers()
          data.sort((a, b) => (a.userName ?? '').localeCompare(b.userName ?? ''))
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [{ type: QueryTags.User, id: 'LIST' }],
    }),

    getUser: builder.query<UserDetailsDto, string>({
      queryFn: async (id: string) => {
        try {
          const data = await getUsersClient().getUser(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result) => [{ type: QueryTags.User, id: result?.id }],
    }),

    getUserRoles: builder.query<
      UserRoleDto[],
      { id: string; includeUnassigned?: boolean }
    >({
      queryFn: async (request) => {
        try {
          const data = await getUsersClient().getRoles(
            request.id,
            request.includeUnassigned ?? false,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        // TODO: should we include includeUnassigned in the tag?
        { type: QueryTags.UserRole, id: arg.id },
      ],
    }),

    manageUserRoles: builder.mutation<void, AssignUserRolesRequest>({
      queryFn: async (request) => {
        try {
          const data = await getUsersClient().manageUserRoles(
            request.userId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => {
        return [
          { type: QueryTags.UserRole, id: arg.userId },
          { type: QueryTags.User, id: arg.userId },
          { type: QueryTags.User, id: 'LIST' },
          { type: QueryTags.RoleUsers },
          { type: QueryTags.RoleUsersCount },
        ]
      },
    }),

    manageRoleUsers: builder.mutation<void, ManageRoleUsersRequest>({
      queryFn: async (request) => {
        try {
          const data = await getUsersClient().manageRoleUsers(request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => {
        return [
          { type: QueryTags.RoleUsers, id: arg.roleId },
          { type: QueryTags.RoleUsersCount, id: arg.roleId },
          { type: QueryTags.User, id: 'LIST' },
          { type: QueryTags.UserRole, id: 'LIST' },
        ]
      },
    }),

    updateUser: builder.mutation<
      void,
      {
        id: string
        firstName: string
        lastName: string
        email: string
        phoneNumber?: string
        employeeId?: string
      }
    >({
      queryFn: async (request) => {
        try {
          const response = await authenticatedFetch(
            `/api/user-management/users/${request.id}`,
            {
              method: 'PUT',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify(request),
            },
          )
          if (!response.ok) {
            const errorData = await response.json()
            return {
              error: {
                status: response.status,
                data: errorData,
              },
            }
          }
          return { data: null as unknown as void }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.User, id: arg.id },
        { type: QueryTags.User, id: 'LIST' },
        { type: QueryTags.UserOption, id: 'LIST' },
      ],
    }),

    createUser: builder.mutation<string, CreateUserRequest>({
      queryFn: async (request) => {
        try {
          const data = await getUsersClient().createUser(request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [
        { type: QueryTags.User, id: 'LIST' },
        { type: QueryTags.UserOption, id: 'LIST' },
      ],
    }),

    resetUserPassword: builder.mutation<
      void,
      { userId: string; newPassword: string }
    >({
      queryFn: async ({ userId, newPassword }) => {
        try {
          const response = await authenticatedFetch(
            `/api/user-management/users/${userId}/reset-password`,
            {
              method: 'PUT',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify({ newPassword }),
            },
          )
          if (!response.ok) {
            const errorData = await response.json()
            return {
              error: {
                status: response.status,
                data: errorData,
              },
            }
          }
          return { data: null as unknown as void }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.User, id: arg.userId },
        { type: QueryTags.User, id: 'LIST' },
      ],
    }),

    activateUser: builder.mutation<void, string>({
      queryFn: async (userId) => {
        try {
          const response = await authenticatedFetch(
            `/api/user-management/users/${userId}/activate`,
            { method: 'PUT' },
          )
          if (!response.ok) {
            const errorData = await response.json()
            return {
              error: {
                status: response.status,
                data: errorData,
              },
            }
          }
          return { data: null as unknown as void }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.User, id: arg },
        { type: QueryTags.User, id: 'LIST' },
        { type: QueryTags.UserOption, id: 'LIST' },
      ],
    }),

    deactivateUser: builder.mutation<void, string>({
      queryFn: async (userId) => {
        try {
          const response = await authenticatedFetch(
            `/api/user-management/users/${userId}/deactivate`,
            { method: 'PUT' },
          )
          if (!response.ok) {
            const errorData = await response.json()
            return {
              error: {
                status: response.status,
                data: errorData,
              },
            }
          }
          return { data: null as unknown as void }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.User, id: arg },
        { type: QueryTags.User, id: 'LIST' },
        { type: QueryTags.UserOption, id: 'LIST' },
      ],
    }),

    unlockUser: builder.mutation<void, string>({
      queryFn: async (userId) => {
        try {
          const response = await authenticatedFetch(
            `/api/user-management/users/${userId}/unlock`,
            {
              method: 'PUT',
            },
          )
          if (!response.ok) {
            const errorData = await response.json()
            return {
              error: {
                status: response.status,
                data: errorData,
              },
            }
          }
          return { data: null as unknown as void }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.User, id: arg },
        { type: QueryTags.User, id: 'LIST' },
      ],
    }),

    getUserIdentityHistory: builder.query<UserIdentityDto[], string>({
      queryFn: async (id: string) => {
        try {
          const data = await getUsersClient().getIdentityHistory(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, id) => [
        { type: QueryTags.UserIdentityHistory, id },
      ],
    }),

    // Staging a tenant migration is now a bulk action on the provider page (see
    // stageBulkTenantMigration in oidc-providers-api). Cancelling a single user's
    // pending migration stays here — there is no bulk-cancel equivalent.
    cancelTenantMigration: builder.mutation<void, string>({
      queryFn: async (userId) => {
        try {
          const data = await getUsersClient().cancelTenantMigration(userId)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, userId) => [
        { type: QueryTags.User, id: userId },
        { type: QueryTags.User, id: 'LIST' },
        { type: QueryTags.UserIdentityHistory, id: userId },
      ],
    }),

    stageProviderMigration: builder.mutation<
      void,
      { userId: string; targetProviderId: string }
    >({
      queryFn: async ({ userId, targetProviderId }) => {
        try {
          const request: StageProviderMigrationRequest = { targetProviderId }
          const data = await getUsersClient().stageProviderMigration(
            userId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.User, id: arg.userId },
        { type: QueryTags.User, id: 'LIST' },
        { type: QueryTags.UserIdentityHistory, id: arg.userId },
      ],
    }),

    cancelProviderMigration: builder.mutation<void, string>({
      queryFn: async (userId) => {
        try {
          const data = await getUsersClient().cancelProviderMigration(userId)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, userId) => [
        { type: QueryTags.User, id: userId },
        { type: QueryTags.User, id: 'LIST' },
        { type: QueryTags.UserIdentityHistory, id: userId },
      ],
    }),

    convertToLocalAccount: builder.mutation<
      void,
      { userId: string; newPassword: string }
    >({
      queryFn: async ({ userId, newPassword }) => {
        try {
          const request: ConvertToLocalAccountRequest = { newPassword }
          const data = await getUsersClient().convertToLocalAccount(
            userId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.User, id: arg.userId },
        { type: QueryTags.User, id: 'LIST' },
        { type: QueryTags.UserIdentityHistory, id: arg.userId },
      ],
    }),

    getUserOptions: builder.query<BaseOptionType[], void>({
      queryFn: async () => {
        try {
          const users = await getUsersClient().getUsers()
          const data: BaseOptionType[] = users
            .map((user) => ({
              label: user.isActive
                ? `${user.firstName} ${user.lastName}`
                : `${user.firstName} ${user.lastName} (Inactive)`,
              value: user.id,
            }))
            .sort((a, b) => a.label.localeCompare(b.label))
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [{ type: QueryTags.UserOption, id: 'LIST' }],
    }),
  }),
})

export const {
  useGetUsersQuery,
  useGetUserQuery,
  useGetUserRolesQuery,
  useManageUserRolesMutation,
  useManageRoleUsersMutation,
  useUpdateUserMutation,
  useCreateUserMutation,
  useResetUserPasswordMutation,
  useActivateUserMutation,
  useDeactivateUserMutation,
  useUnlockUserMutation,
  useGetUserIdentityHistoryQuery,
  useCancelTenantMigrationMutation,
  useStageProviderMigrationMutation,
  useCancelProviderMigrationMutation,
  useConvertToLocalAccountMutation,
  useGetUserOptionsQuery,
} = usersApi

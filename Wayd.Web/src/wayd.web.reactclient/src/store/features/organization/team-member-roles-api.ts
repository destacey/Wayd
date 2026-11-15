import {
  teamMemberRolesActivate,
  teamMemberRolesCreate,
  teamMemberRolesDeactivate,
  teamMemberRolesDelete,
  teamMemberRolesGetList,
  teamMemberRolesUpdate,
} from '@/src/services/api/team-member-roles'
import {
  CreateTeamMemberRoleRequest,
  TeamMemberRoleDto,
  UpdateTeamMemberRoleRequest,
} from '@/src/services/api/wayd.schemas'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'

export const teamMemberRolesApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getTeamMemberRoles: builder.query<TeamMemberRoleDto[], boolean | undefined>(
      {
        queryFn: async (includeInactive = false) => {
          try {
            const data = await teamMemberRolesGetList({
              includeInactive: includeInactive,
            })
            return { data }
          } catch (error) {
            console.error('API Error:', error)
            return { error }
          }
        },
        providesTags: () => [{ type: QueryTags.TeamMemberRole, id: 'LIST' }],
      },
    ),
    createTeamMemberRole: builder.mutation<string, CreateTeamMemberRoleRequest>(
      {
        queryFn: async (request) => {
          try {
            const data = await teamMemberRolesCreate(request)
            return { data }
          } catch (error) {
            console.error('API Error:', error)
            return { error }
          }
        },
        invalidatesTags: () => [{ type: QueryTags.TeamMemberRole, id: 'LIST' }],
      },
    ),
    updateTeamMemberRole: builder.mutation<void, UpdateTeamMemberRoleRequest>({
      queryFn: async (request) => {
        try {
          const data = await teamMemberRolesUpdate(request.id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.TeamMemberRole, id: 'LIST' },
        { type: QueryTags.TeamMemberRole, id: arg.id },
      ],
    }),
    deleteTeamMemberRole: builder.mutation<void, string>({
      queryFn: async (id) => {
        try {
          const data = await teamMemberRolesDelete(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [{ type: QueryTags.TeamMemberRole, id: 'LIST' }],
    }),
    activateTeamMemberRole: builder.mutation<void, string>({
      queryFn: async (id) => {
        try {
          const data = await teamMemberRolesActivate(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.TeamMemberRole, id: 'LIST' },
        { type: QueryTags.TeamMemberRole, id: arg },
      ],
    }),
    deactivateTeamMemberRole: builder.mutation<void, string>({
      queryFn: async (id) => {
        try {
          const data = await teamMemberRolesDeactivate(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.TeamMemberRole, id: 'LIST' },
        { type: QueryTags.TeamMemberRole, id: arg },
      ],
    }),
  }),
})

export const {
  useGetTeamMemberRolesQuery,
  useCreateTeamMemberRoleMutation,
  useUpdateTeamMemberRoleMutation,
  useDeleteTeamMemberRoleMutation,
  useActivateTeamMemberRoleMutation,
  useDeactivateTeamMemberRoleMutation,
} = teamMemberRolesApi

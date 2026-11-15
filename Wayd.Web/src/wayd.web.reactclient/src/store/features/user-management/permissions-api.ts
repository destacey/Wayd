import { ApplicationPermission } from '@/src/services/api/wayd.schemas'
import { apiSlice } from '../apiSlice'
import { permissionsGetList } from '@/src/services/api/permissions'
import { QueryTags } from '../query-tags'

export const permissionsApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getPermissions: builder.query<ApplicationPermission[], void>({
      queryFn: async () => {
        try {
          const data = await permissionsGetList()
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [{ type: QueryTags.Permission, id: 'LIST' }],
      // Cache permissions for 5 minutes since they don't change except on new deployments
      keepUnusedDataFor: 300,
    }),
  }),
})

export const { useGetPermissionsQuery } = permissionsApi

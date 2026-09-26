import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import {
  PagedResponseOfActivityLogDto,
  SchedulingSettingsDto,
  UpdateSchedulingSettingsRequest,
} from '@/src/services/wayd-api'
import { getSystemSettingsClient } from '@/src/services/clients'
import { ActivityLogQueryArg } from '@/src/components/common/activities'

/** The Activity tag id for the scheduling section; a section is a singleton, so its key is its id. */
export const SCHEDULING_SETTINGS_ACTIVITY_ID = 'system-settings-scheduling'

export const systemSettingsApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getSchedulingSettings: builder.query<SchedulingSettingsDto, void>({
      queryFn: async () => {
        try {
          const data = await getSystemSettingsClient().getSchedulingSettings()
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return {
            error:
              error ?? new Error('Unknown error loading scheduling settings'),
          }
        }
      },
      providesTags: [{ type: QueryTags.SystemSettings, id: 'scheduling' }],
    }),
    updateSchedulingSettings: builder.mutation<
      void,
      UpdateSchedulingSettingsRequest
    >({
      queryFn: async (request) => {
        try {
          await getSystemSettingsClient().updateSchedulingSettings(request)
          return { data: null as unknown as void }
        } catch (error) {
          console.error('API Error:', error)
          return {
            error:
              error ?? new Error('Unknown error updating scheduling settings'),
          }
        }
      },
      invalidatesTags: [
        { type: QueryTags.SystemSettings, id: 'scheduling' },
        { type: QueryTags.ActivityLog, id: SCHEDULING_SETTINGS_ACTIVITY_ID },
      ],
    }),
    // Takes the shared activity-log argument so useActivityLog can page it; the id is fixed.
    getSchedulingSettingsActivities: builder.query<
      PagedResponseOfActivityLogDto,
      ActivityLogQueryArg
    >({
      queryFn: async ({ page, pageSize }) => {
        try {
          const data =
            await getSystemSettingsClient().getSchedulingSettingsActivities(
              page,
              pageSize,
            )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: [
        { type: QueryTags.ActivityLog, id: SCHEDULING_SETTINGS_ACTIVITY_ID },
      ],
    }),
  }),
})

export const {
  useGetSchedulingSettingsQuery,
  useUpdateSchedulingSettingsMutation,
  useGetSchedulingSettingsActivitiesQuery,
  useLazyGetSchedulingSettingsActivitiesQuery,
} = systemSettingsApi

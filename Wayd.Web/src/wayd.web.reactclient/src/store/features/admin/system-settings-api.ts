import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import {
  GeneralSettingsDto,
  PagedResponseOfActivityLogDto,
  SchedulingSettingsDto,
  SprintMetricsSettingsDto,
  UpdateGeneralSettingsRequest,
  UpdateProductTagSettingsRequest,
  UpdateSchedulingSettingsRequest,
  UpdateSprintMetricsSettingsRequest,
} from '@/src/services/api/wayd.schemas'
import {
  systemSettingsGetGeneralSettings,
  systemSettingsGetGeneralSettingsActivities,
  systemSettingsGetProductTagSettingsActivities,
  systemSettingsGetSchedulingSettings,
  systemSettingsGetSchedulingSettingsActivities,
  systemSettingsGetSprintMetricsSettings,
  systemSettingsGetSprintMetricsSettingsActivities,
  systemSettingsUpdateGeneralSettings,
  systemSettingsUpdateProductTagSettings,
  systemSettingsUpdateSchedulingSettings,
  systemSettingsUpdateSprintMetricsSettings,
} from '@/src/services/api/system-settings'
import { ActivityLogQueryArg } from '@/src/components/common/activities'

/** The Activity tag id for the general section; a section is a singleton, so its key is its id. */
export const GENERAL_SETTINGS_ACTIVITY_ID = 'system-settings-general'

/** The Activity tag id for the scheduling section; a section is a singleton, so its key is its id. */
export const SCHEDULING_SETTINGS_ACTIVITY_ID = 'system-settings-scheduling'

/** The Activity tag id for the sprint metrics section. */
export const SPRINT_METRICS_SETTINGS_ACTIVITY_ID =
  'system-settings-sprint-metrics'

/** The Activity tag id for the product tags section. */
export const PRODUCT_TAG_SETTINGS_ACTIVITY_ID = 'system-settings-product-tags'

export const systemSettingsApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getGeneralSettings: builder.query<GeneralSettingsDto, void>({
      queryFn: async () => {
        try {
          const data = await systemSettingsGetGeneralSettings()
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return {
            error: error ?? new Error('Unknown error loading general settings'),
          }
        }
      },
      providesTags: [{ type: QueryTags.SystemSettings, id: 'general' }],
    }),
    updateGeneralSettings: builder.mutation<void, UpdateGeneralSettingsRequest>(
      {
        queryFn: async (request) => {
          try {
            await systemSettingsUpdateGeneralSettings(request)
            return { data: null as unknown as void }
          } catch (error) {
            console.error('API Error:', error)
            return {
              error:
                error ?? new Error('Unknown error updating general settings'),
            }
          }
        },
        invalidatesTags: [
          { type: QueryTags.SystemSettings, id: 'general' },
          { type: QueryTags.ActivityLog, id: GENERAL_SETTINGS_ACTIVITY_ID },
        ],
      },
    ),
    // Takes the shared activity-log argument so useActivityLog can page it; the id is fixed.
    getGeneralSettingsActivities: builder.query<
      PagedResponseOfActivityLogDto,
      ActivityLogQueryArg
    >({
      queryFn: async ({ page, pageSize }) => {
        try {
          const data = await systemSettingsGetGeneralSettingsActivities({
            page: page,
            pageSize: pageSize,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: [
        { type: QueryTags.ActivityLog, id: GENERAL_SETTINGS_ACTIVITY_ID },
      ],
    }),
    getSchedulingSettings: builder.query<SchedulingSettingsDto, void>({
      queryFn: async () => {
        try {
          const data = await systemSettingsGetSchedulingSettings()
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
          await systemSettingsUpdateSchedulingSettings(request)
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
        // Which calendar is the default shows on the calendars themselves.
        QueryTags.HolidayCalendar,
      ],
    }),
    // Takes the shared activity-log argument so useActivityLog can page it; the id is fixed.
    getSchedulingSettingsActivities: builder.query<
      PagedResponseOfActivityLogDto,
      ActivityLogQueryArg
    >({
      queryFn: async ({ page, pageSize }) => {
        try {
          const data = await systemSettingsGetSchedulingSettingsActivities({
            page: page,
            pageSize: pageSize,
          })
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
    getSprintMetricsSettings: builder.query<SprintMetricsSettingsDto, void>({
      queryFn: async () => {
        try {
          const data = await systemSettingsGetSprintMetricsSettings()
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return {
            error:
              error ??
              new Error('Unknown error loading sprint metrics settings'),
          }
        }
      },
      providesTags: [{ type: QueryTags.SystemSettings, id: 'sprint-metrics' }],
    }),
    updateSprintMetricsSettings: builder.mutation<
      void,
      UpdateSprintMetricsSettingsRequest
    >({
      queryFn: async (request) => {
        try {
          await systemSettingsUpdateSprintMetricsSettings(request)
          return { data: null as unknown as void }
        } catch (error) {
          console.error('API Error:', error)
          return {
            error:
              error ??
              new Error('Unknown error updating sprint metrics settings'),
          }
        }
      },
      invalidatesTags: [
        { type: QueryTags.SystemSettings, id: 'sprint-metrics' },
        {
          type: QueryTags.ActivityLog,
          id: SPRINT_METRICS_SETTINGS_ACTIVITY_ID,
        },
      ],
    }),
    // Takes the shared activity-log argument so useActivityLog can page it; the id is fixed.
    getSprintMetricsSettingsActivities: builder.query<
      PagedResponseOfActivityLogDto,
      ActivityLogQueryArg
    >({
      queryFn: async ({ page, pageSize }) => {
        try {
          const data = await systemSettingsGetSprintMetricsSettingsActivities({
            page: page,
            pageSize: pageSize,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: [
        {
          type: QueryTags.ActivityLog,
          id: SPRINT_METRICS_SETTINGS_ACTIVITY_ID,
        },
      ],
    }),
    updateProductTagSettings: builder.mutation<
      void,
      UpdateProductTagSettingsRequest
    >({
      queryFn: async (request) => {
        try {
          await systemSettingsUpdateProductTagSettings(request)
          return { data: null as unknown as void }
        } catch (error) {
          console.error('API Error:', error)
          return {
            error:
              error ?? new Error('Unknown error updating product tag settings'),
          }
        }
      },
      invalidatesTags: [
        { type: QueryTags.SystemSettings, id: 'product-tags' },
        { type: QueryTags.ActivityLog, id: PRODUCT_TAG_SETTINGS_ACTIVITY_ID },
      ],
    }),
    // Takes the shared activity-log argument so useActivityLog can page it; the id is fixed.
    getProductTagSettingsActivities: builder.query<
      PagedResponseOfActivityLogDto,
      ActivityLogQueryArg
    >({
      queryFn: async ({ page, pageSize }) => {
        try {
          const data = await systemSettingsGetProductTagSettingsActivities({
            page: page,
            pageSize: pageSize,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: [
        { type: QueryTags.ActivityLog, id: PRODUCT_TAG_SETTINGS_ACTIVITY_ID },
      ],
    }),
  }),
})

export const {
  useGetGeneralSettingsQuery,
  useUpdateGeneralSettingsMutation,
  useGetGeneralSettingsActivitiesQuery,
  useLazyGetGeneralSettingsActivitiesQuery,
  useGetSprintMetricsSettingsQuery,
  useUpdateSprintMetricsSettingsMutation,
  useGetSprintMetricsSettingsActivitiesQuery,
  useLazyGetSprintMetricsSettingsActivitiesQuery,
  useGetSchedulingSettingsQuery,
  useUpdateSchedulingSettingsMutation,
  useGetSchedulingSettingsActivitiesQuery,
  useLazyGetSchedulingSettingsActivitiesQuery,
  useUpdateProductTagSettingsMutation,
  useGetProductTagSettingsActivitiesQuery,
  useLazyGetProductTagSettingsActivitiesQuery,
} = systemSettingsApi

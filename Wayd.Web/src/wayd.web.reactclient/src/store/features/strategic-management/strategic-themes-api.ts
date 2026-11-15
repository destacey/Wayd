import { apiSlice } from '../apiSlice'
import {
  CreateStrategicThemeRequest,
  ObjectIdAndKey,
  StrategicThemeDetailsDto,
  StrategicThemeListDto,
  UpdateStrategicThemeRequest,
  PagedResponseOfActivityLogDto,
  StrategicThemeState,
} from '@/src/services/api/wayd.schemas'
import { strategicThemesGetStrategicThemes } from '@/src/services/api/strategic-themes'
import {
  strategicThemesActivate,
  strategicThemesArchive,
  strategicThemesCreate,
  strategicThemesDelete,
  strategicThemesGetActivities,
  strategicThemesGetStateOptions,
  strategicThemesGetStrategicTheme,
  strategicThemesGetStrategicThemeOptions,
  strategicThemesUpdate,
} from '@/src/services/api/strategic-themes'
import { QueryTags } from '../query-tags'
import { OptionModel } from '@/src/components/types'
import type { BaseOptionType } from 'antd/es/select'

export const strategicThemesApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getStrategicThemes: builder.query<
      StrategicThemeListDto[],
      { state?: StrategicThemeState[] } | undefined
    >({
      queryFn: async (request = undefined) => {
        try {
          const data = await strategicThemesGetStrategicThemes({
            state:
              (request?.state?.length ?? 0) > 0 ? request!.state : undefined,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [{ type: QueryTags.StrategicTheme, id: 'LIST' }],
    }),
    getStrategicTheme: builder.query<StrategicThemeDetailsDto, number>({
      queryFn: async (key) => {
        try {
          const data = await strategicThemesGetStrategicTheme(key.toString())
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.StrategicTheme, id: arg },
      ],
    }),
    createStrategicTheme: builder.mutation<
      ObjectIdAndKey,
      CreateStrategicThemeRequest
    >({
      queryFn: async (request) => {
        try {
          const data = await strategicThemesCreate(request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => {
        return [{ type: QueryTags.StrategicTheme, id: 'LIST' }]
      },
    }),
    updateStrategicTheme: builder.mutation<
      void,
      { request: UpdateStrategicThemeRequest; cacheKey: number }
    >({
      queryFn: async ({ request, cacheKey }) => {
        try {
          const data = await strategicThemesUpdate(request.id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { cacheKey }) => {
        return [
          { type: QueryTags.StrategicTheme, id: 'LIST' },
          { type: QueryTags.StrategicTheme, id: cacheKey },
        ]
      },
    }),
    activateStrategicTheme: builder.mutation<
      void,
      { id: string; cacheKey: number }
    >({
      queryFn: async ({ id }) => {
        try {
          const data = await strategicThemesActivate(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { cacheKey }) => {
        return [
          { type: QueryTags.StrategicTheme, id: 'LIST' },
          { type: QueryTags.StrategicTheme, id: cacheKey },
        ]
      },
    }),
    archiveStrategicTheme: builder.mutation<
      void,
      { id: string; cacheKey: number }
    >({
      queryFn: async ({ id }) => {
        try {
          const data = await strategicThemesArchive(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { cacheKey }) => {
        return [
          { type: QueryTags.StrategicTheme, id: 'LIST' },
          { type: QueryTags.StrategicTheme, id: cacheKey },
        ]
      },
    }),
    deleteStrategicTheme: builder.mutation<
      void,
      { strategicThemeId: string; cacheKey: number }
    >({
      queryFn: async ({ strategicThemeId }) => {
        try {
          const data = await strategicThemesDelete(strategicThemeId)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { cacheKey }) => {
        return [{ type: QueryTags.StrategicTheme, id: 'LIST' }]
      },
    }),
    getStateOptions: builder.query<OptionModel<StrategicThemeState>[], void>({
      queryFn: async () => {
        try {
          const states = await strategicThemesGetStateOptions()

          const data: OptionModel<StrategicThemeState>[] = states
            .sort((a, b) => a.order - b.order)
            .map((s) => ({
              value: s.code,
              label: s.name,
            }))

          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [QueryTags.StrategicThemState],
    }),
    getStrategicThemeOptions: builder.query<
      BaseOptionType[],
      boolean | undefined
    >({
      queryFn: async (includeArchived = false) => {
        try {
          const themes = await strategicThemesGetStrategicThemeOptions({
            includeArchived: includeArchived,
          })

          const data: BaseOptionType[] = themes
            .sort((a, b) => a.name.localeCompare(b.name))
            .map((category) => ({
              label: category.name,
              value: category.id,
            }))

          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
    }),

    getStrategicThemeActivities: builder.query<
      PagedResponseOfActivityLogDto,
      { idOrKey: string | number; page?: number; pageSize?: number }
    >({
      queryFn: async ({ idOrKey, page, pageSize }) => {
        try {
          const data = await strategicThemesGetActivities(String(idOrKey), {
            page: page,
            pageSize: pageSize,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, { idOrKey }) => [
        { type: QueryTags.ActivityLog, id: String(idOrKey) },
      ],
    }),
  }),
})

export const {
  useGetStrategicThemesQuery,
  useGetStrategicThemeQuery,
  useCreateStrategicThemeMutation,
  useUpdateStrategicThemeMutation,
  useActivateStrategicThemeMutation,
  useArchiveStrategicThemeMutation,
  useDeleteStrategicThemeMutation,
  useGetStateOptionsQuery,
  useGetStrategicThemeOptionsQuery,
  useGetStrategicThemeActivitiesQuery,
  useLazyGetStrategicThemeActivitiesQuery,
} = strategicThemesApi

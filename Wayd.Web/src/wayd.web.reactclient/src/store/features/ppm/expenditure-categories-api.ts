import { getExpenditureCategoriesClient } from '@/src/services/clients'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import {
  CreateExpenditureCategoryRequest,
  ExpenditureCategoryDetailsDto,
  ExpenditureCategoryListDto,
  UpdateExpenditureCategoryRequest,
} from '@/src/services/wayd-api'
import type { BaseOptionType } from 'antd/es/select'

export const expenditureCategoriesApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getExpenditureCategories: builder.query<ExpenditureCategoryListDto[], void>(
      {
        queryFn: async () => {
          try {
            const data =
              await getExpenditureCategoriesClient().getExpenditureCategories()
            return { data }
          } catch (error) {
            console.error('API Error:', error)
            return { error }
          }
        },
        providesTags: () => [
          { type: QueryTags.ExpenditureCategory, id: 'LIST' },
        ],
      },
    ),
    getExpenditureCategory: builder.query<
      ExpenditureCategoryDetailsDto,
      number
    >({
      queryFn: async (id) => {
        try {
          const data =
            await getExpenditureCategoriesClient().getExpenditureCategory(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.ExpenditureCategory, id: arg },
      ],
    }),
    createExpenditureCategory: builder.mutation<
      number,
      CreateExpenditureCategoryRequest
    >({
      queryFn: async (request) => {
        try {
          const data = await getExpenditureCategoriesClient().create(request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => {
        return [{ type: QueryTags.ExpenditureCategory, id: 'LIST' }]
      },
    }),
    updateExpenditureCategory: builder.mutation<
      void,
      UpdateExpenditureCategoryRequest
    >({
      queryFn: async (request) => {
        try {
          const data = await getExpenditureCategoriesClient().update(
            request.id,
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
          { type: QueryTags.ExpenditureCategory, id: 'LIST' },
          { type: QueryTags.ExpenditureCategory, id: arg.id },
        ]
      },
    }),
    activateExpenditureCategory: builder.mutation<void, number>({
      queryFn: async (id) => {
        try {
          const data = await getExpenditureCategoriesClient().activate(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => {
        return [
          { type: QueryTags.ExpenditureCategory, id: 'LIST' },
          { type: QueryTags.ExpenditureCategory, id: arg },
        ]
      },
    }),
    archiveExpenditureCategory: builder.mutation<void, number>({
      queryFn: async (id) => {
        try {
          const data = await getExpenditureCategoriesClient().archive(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => {
        return [
          { type: QueryTags.ExpenditureCategory, id: 'LIST' },
          { type: QueryTags.ExpenditureCategory, id: arg },
        ]
      },
    }),
    deleteExpenditureCategory: builder.mutation<void, number>({
      queryFn: async (id) => {
        try {
          const data = await getExpenditureCategoriesClient().delete(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => {
        return [{ type: QueryTags.ExpenditureCategory, id: 'LIST' }]
      },
    }),
    getExpenditureCategoryOptions: builder.query<
      BaseOptionType[],
      boolean | undefined
    >({
      queryFn: async (includeArchived = false) => {
        try {
          const categories =
            await getExpenditureCategoriesClient().getExpenditureCategoryOptions(
              includeArchived,
            )

          const data: BaseOptionType[] = categories
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
  }),
})

export const {
  useGetExpenditureCategoriesQuery,
  useGetExpenditureCategoryQuery,
  useCreateExpenditureCategoryMutation,
  useUpdateExpenditureCategoryMutation,
  useActivateExpenditureCategoryMutation,
  useArchiveExpenditureCategoryMutation,
  useDeleteExpenditureCategoryMutation,
  useGetExpenditureCategoryOptionsQuery,
} = expenditureCategoriesApi

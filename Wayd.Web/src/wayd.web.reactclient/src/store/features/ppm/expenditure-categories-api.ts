import {
  expenditureCategoriesActivate,
  expenditureCategoriesArchive,
  expenditureCategoriesCreate,
  expenditureCategoriesDelete,
  expenditureCategoriesGetExpenditureCategories,
  expenditureCategoriesGetExpenditureCategory,
  expenditureCategoriesGetExpenditureCategoryOptions,
  expenditureCategoriesUpdate,
} from '@/src/services/api/expenditure-categories'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import {
  CreateExpenditureCategoryRequest,
  ExpenditureCategoryDetailsDto,
  ExpenditureCategoryListDto,
  UpdateExpenditureCategoryRequest,
} from '@/src/services/api/wayd.schemas'
import type { BaseOptionType } from 'antd/es/select'

export const expenditureCategoriesApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getExpenditureCategories: builder.query<ExpenditureCategoryListDto[], void>(
      {
        queryFn: async () => {
          try {
            const data = await expenditureCategoriesGetExpenditureCategories()
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
          const data = await expenditureCategoriesGetExpenditureCategory(id)
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
          const data = await expenditureCategoriesCreate(request)
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
          const data = await expenditureCategoriesUpdate(request.id, request)
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
          const data = await expenditureCategoriesActivate(id)
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
          const data = await expenditureCategoriesArchive(id)
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
          const data = await expenditureCategoriesDelete(id)
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
            await expenditureCategoriesGetExpenditureCategoryOptions({
              includeArchived: includeArchived,
            })

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

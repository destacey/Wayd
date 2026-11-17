import {
  kpiCategoriesActivate,
  kpiCategoriesCreate,
  kpiCategoriesDeactivate,
  kpiCategoriesDelete,
  kpiCategoriesGetCategories,
  kpiCategoriesReorder,
  kpiCategoriesUpdate,
} from '@/src/services/api/kpi-categories'
import {
  kpisAddKpi,
  kpisChangeMeasurement,
  kpisClose,
  kpisGetClosed,
  kpisGetKpi,
  kpisGetScorecard,
  kpisRecordMeasurement,
  kpisRemove,
  kpisRemoveMeasurement,
  kpisReopen,
  kpisReorder,
  kpisUpdateKpi,
} from '@/src/services/api/kpis'
import {
  KpiCategoryDto,
  KpiCategoryRequest,
  KpiDetailsDto,
  KpiMeasurementRequest,
  KpiRequest,
  KpiScorecardDto,
  KpiSummaryDto,
  ObjectIdAndKey,
} from '@/src/services/api/wayd.schemas'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'

/**
 * The kinds of record that can carry KPIs, as the API names them. Each is
 * registered on the server with the rules for who may read and change them.
 */
export const KpiSubjectTypes = {
  Team: 'organization.team',
} as const

export type KpiSubjectType =
  (typeof KpiSubjectTypes)[keyof typeof KpiSubjectTypes]

/** The record a scorecard belongs to. */
export interface KpiSubject {
  subjectType: KpiSubjectType
  subjectId: string
}

/** One KPI on a record. */
export interface KpiTarget extends KpiSubject {
  kpiId: string
}

/** One measurement on a KPI. */
export interface KpiMeasurementTarget extends KpiTarget {
  measurementId: string
}

const scorecardTag = ({ subjectType, subjectId }: KpiSubject) => ({
  type: QueryTags.KpiScorecard,
  id: `${subjectType}:${subjectId}`,
})

const kpiTag = (kpiId: string) => ({ type: QueryTags.Kpi, id: kpiId })

/** The categories list, which counts the KPIs in each category. */
const categoryListTag = { type: QueryTags.KpiCategory, id: 'LIST' } as const

/**
 * What changing a record's KPIs refreshes: its scorecard and closed list, and
 * its Activity, where every KPI event is filed beside the KPI's own.
 */
const subjectWriteTags = (subject: KpiSubject) => [
  scorecardTag(subject),
  { type: QueryTags.ActivityLog, id: subject.subjectId },
]

const kpiWriteTags = (target: KpiTarget) => [
  ...subjectWriteTags(target),
  kpiTag(target.kpiId),
]

const run = async <T>(call: () => Promise<T>) => {
  try {
    return { data: await call() }
  } catch (error) {
    console.error('API Error:', error)
    return { error }
  }
}

/** For an endpoint that answers 204: RTK Query rejects undefined as a result. */
const runVoid = async (call: () => Promise<void>) => {
  try {
    await call()
    return { data: null }
  } catch (error) {
    console.error('API Error:', error)
    return { error }
  }
}

export const kpisApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getKpiScorecard: builder.query<KpiScorecardDto, KpiSubject>({
      queryFn: ({ subjectType, subjectId }) =>
        run(() => kpisGetScorecard(subjectType, subjectId)),
      providesTags: (result, error, args) => [scorecardTag(args)],
    }),

    getClosedKpis: builder.query<KpiSummaryDto[], KpiSubject>({
      queryFn: ({ subjectType, subjectId }) =>
        run(() => kpisGetClosed(subjectType, subjectId)),
      providesTags: (result, error, args) => [scorecardTag(args)],
    }),

    getKpi: builder.query<KpiDetailsDto, KpiTarget>({
      queryFn: ({ subjectType, subjectId, kpiId }) =>
        run(() => kpisGetKpi(subjectType, subjectId, kpiId)),
      providesTags: (result, error, args) => [kpiTag(args.kpiId)],
    }),

    addKpi: builder.mutation<
      ObjectIdAndKey,
      KpiSubject & { request: KpiRequest }
    >({
      queryFn: ({ subjectType, subjectId, request }) =>
        run(() => kpisAddKpi(subjectType, subjectId, request)),
      invalidatesTags: (result, error, args) => [
        ...subjectWriteTags(args),
        categoryListTag,
      ],
    }),

    // One save for the details and the goal, which the server checks as a whole.
    updateKpi: builder.mutation<null, KpiTarget & { request: KpiRequest }>({
      queryFn: ({ subjectType, subjectId, kpiId, request }) =>
        runVoid(() => kpisUpdateKpi(subjectType, subjectId, kpiId, request)),
      invalidatesTags: (result, error, args) => [
        ...kpiWriteTags(args),
        categoryListTag,
      ],
    }),

    closeKpi: builder.mutation<null, KpiTarget>({
      queryFn: ({ subjectType, subjectId, kpiId }) =>
        runVoid(() => kpisClose(subjectType, subjectId, kpiId)),
      invalidatesTags: (result, error, args) => kpiWriteTags(args),
    }),

    reopenKpi: builder.mutation<null, KpiTarget>({
      queryFn: ({ subjectType, subjectId, kpiId }) =>
        runVoid(() => kpisReopen(subjectType, subjectId, kpiId)),
      invalidatesTags: (result, error, args) => kpiWriteTags(args),
    }),

    removeKpi: builder.mutation<null, KpiTarget>({
      queryFn: ({ subjectType, subjectId, kpiId }) =>
        runVoid(() => kpisRemove(subjectType, subjectId, kpiId)),
      // Not the KPI's own tag: the drawer still showing it would refetch a KPI
      // that no longer exists.
      invalidatesTags: (result, error, args) => [
        ...subjectWriteTags(args),
        categoryListTag,
      ],
    }),

    reorderKpis: builder.mutation<null, KpiSubject & { kpiIds: string[] }>({
      queryFn: ({ subjectType, subjectId, kpiIds }) =>
        runVoid(() => kpisReorder(subjectType, subjectId, { kpiIds })),
      invalidatesTags: (result, error, args) => subjectWriteTags(args),
    }),

    recordKpiMeasurement: builder.mutation<
      string,
      KpiTarget & { request: KpiMeasurementRequest }
    >({
      queryFn: ({ subjectType, subjectId, kpiId, request }) =>
        run(() =>
          kpisRecordMeasurement(subjectType, subjectId, kpiId, request),
        ),
      invalidatesTags: (result, error, args) => kpiWriteTags(args),
    }),

    changeKpiMeasurement: builder.mutation<
      null,
      KpiMeasurementTarget & { request: KpiMeasurementRequest }
    >({
      queryFn: ({ subjectType, subjectId, kpiId, measurementId, request }) =>
        runVoid(() =>
          kpisChangeMeasurement(
            subjectType,
            subjectId,
            kpiId,
            measurementId,
            request,
          ),
        ),
      invalidatesTags: (result, error, args) => kpiWriteTags(args),
    }),

    removeKpiMeasurement: builder.mutation<null, KpiMeasurementTarget>({
      queryFn: ({ subjectType, subjectId, kpiId, measurementId }) =>
        runVoid(() =>
          kpisRemoveMeasurement(subjectType, subjectId, kpiId, measurementId),
        ),
      invalidatesTags: (result, error, args) => kpiWriteTags(args),
    }),

    getKpiCategories: builder.query<
      KpiCategoryDto[],
      { includeInactive: boolean }
    >({
      queryFn: ({ includeInactive }) =>
        run(() => kpiCategoriesGetCategories({ includeInactive })),
      providesTags: [categoryListTag],
    }),

    createKpiCategory: builder.mutation<string, KpiCategoryRequest>({
      queryFn: (request) => run(() => kpiCategoriesCreate(request)),
      invalidatesTags: [categoryListTag],
    }),

    // A category's name and order show on every scorecard that groups by it.
    updateKpiCategory: builder.mutation<
      null,
      { id: string; request: KpiCategoryRequest }
    >({
      queryFn: ({ id, request }) =>
        runVoid(() => kpiCategoriesUpdate(id, request)),
      invalidatesTags: [categoryListTag, QueryTags.KpiScorecard, QueryTags.Kpi],
    }),

    setKpiCategoryActive: builder.mutation<
      null,
      { id: string; isActive: boolean }
    >({
      queryFn: ({ id, isActive }) =>
        runVoid(() =>
          isActive ? kpiCategoriesActivate(id) : kpiCategoriesDeactivate(id),
        ),
      invalidatesTags: [categoryListTag],
    }),

    deleteKpiCategory: builder.mutation<null, string>({
      queryFn: (id) => runVoid(() => kpiCategoriesDelete(id)),
      invalidatesTags: [categoryListTag],
    }),

    reorderKpiCategories: builder.mutation<null, string[]>({
      queryFn: (categoryIds) =>
        runVoid(() => kpiCategoriesReorder({ categoryIds })),
      invalidatesTags: [categoryListTag, QueryTags.KpiScorecard, QueryTags.Kpi],
    }),
  }),
})

export const {
  useGetKpiScorecardQuery,
  useGetClosedKpisQuery,
  useGetKpiQuery,
  useAddKpiMutation,
  useUpdateKpiMutation,
  useCloseKpiMutation,
  useReopenKpiMutation,
  useRemoveKpiMutation,
  useReorderKpisMutation,
  useRecordKpiMeasurementMutation,
  useChangeKpiMeasurementMutation,
  useRemoveKpiMeasurementMutation,
  useGetKpiCategoriesQuery,
  useCreateKpiCategoryMutation,
  useUpdateKpiCategoryMutation,
  useSetKpiCategoryActiveMutation,
  useDeleteKpiCategoryMutation,
  useReorderKpiCategoriesMutation,
} = kpisApi

import {
  CreateRoadmapActivityRequest,
  CreateRoadmapMilestoneRequest,
  CreateRoadmapTimeboxRequest,
  ObjectIdAndKey,
  UpdateRoadmapActivityPlacementRequest,
  RoadmapActivityListDto,
  RoadmapItemDetailsDto,
  RoadmapItemListDto,
  RoadmapMilestoneListDto,
  RoadmapTimeboxListDto,
  UpdateRoadmapActivityDatesRequest,
  UpdateRoadmapActivityRequest,
  UpdateRoadmapMilestoneDatesRequest,
  UpdateRoadmapMilestoneRequest,
  UpdateRoadmapTimeboxDatesRequest,
  UpdateRoadmapTimeboxRequest,
} from './../../../services/wayd-api'
import {
  CopyRoadmapRequest,
  CreateRoadmapRequest,
  RoadmapDetailsDto,
  RoadmapListDto,
  UpdateRoadmapColorsRequest,
  UpdateRoadmapRequest,
} from '@/src/services/wayd-api'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import { authenticatedFetch, getRoadmapsClient } from '@/src/services/clients'
import { OptionModel } from '@/src/components/types'

export const ROADMAP_STATE = {
  Active: 1,
  Archived: 2,
} as const

// Walks the roadmap-items tree (children live under activities) and mutates
// the item whose id matches the date-update request. The NSwag-generated client
// types these fields as `Date` but does not convert them — `processGetItems`
// returns raw JSON, so date fields are actually ISO strings at runtime. Write
// strings here to match the post-refetch shape; downstream consumers parse with
// dayjs() which accepts both. (Storing Dates would also trip the Redux
// serializability check on the patch action.)
function applyOptimisticDates(
  items: RoadmapItemListDto[] | undefined,
  request:
    | UpdateRoadmapActivityDatesRequest
    | UpdateRoadmapMilestoneDatesRequest
    | UpdateRoadmapTimeboxDatesRequest,
): boolean {
  if (!items) return false
  for (const item of items) {
    if (item.id === request.itemId) {
      if ('date' in request && request.date !== undefined) {
        ;(item as RoadmapMilestoneListDto).date = toIsoDateString(request.date)
      }
      if ('start' in request && request.start !== undefined) {
        ;(item as RoadmapActivityListDto | RoadmapTimeboxListDto).start =
          toIsoDateString(request.start)
      }
      if ('end' in request && request.end !== undefined) {
        ;(item as RoadmapActivityListDto | RoadmapTimeboxListDto).end =
          toIsoDateString(request.end)
      }
      return true
    }
    const activity = item as RoadmapActivityListDto
    if (activity.children && applyOptimisticDates(activity.children, request)) {
      return true
    }
  }
  return false
}

// The DTO fields are typed as `Date` but at runtime hold ISO strings. The
// request may carry either a real Date (rare) or a YYYY-MM-DD string cast to
// Date by the timeline consumer. Normalize to a string that vis-timeline /
// dayjs can parse, and cast back to `Date` to satisfy the DTO type.
function toIsoDateString(value: Date | string): Date {
  if (typeof value === 'string') return value as unknown as Date
  if (value instanceof Date)
    return value.toISOString() as unknown as Date
  return value
}

export const roadmapApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getRoadmaps: builder.query<
      RoadmapListDto[],
      { state?: number[] } | undefined
    >({
      queryFn: async (request = undefined) => {
        try {
          const data = await getRoadmapsClient().getRoadmaps(
            (request?.state?.length ?? 0) > 0 ? request!.state : undefined,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [{ type: QueryTags.Roadmap, id: 'LIST' }],
    }),
    getRoadmap: builder.query<RoadmapDetailsDto, string>({
      queryFn: async (idOrKey: string) => {
        try {
          const data = await getRoadmapsClient().getRoadmap(idOrKey)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result) => [{ type: QueryTags.Roadmap, id: result?.key }],
    }),
    createRoadmap: builder.mutation<ObjectIdAndKey, CreateRoadmapRequest>({
      queryFn: async (request) => {
        try {
          const data = await getRoadmapsClient().create(request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => {
        return [{ type: QueryTags.Roadmap, id: 'LIST' }]
      },
    }),
    copyRoadmap: builder.mutation<ObjectIdAndKey, CopyRoadmapRequest>({
      queryFn: async (request) => {
        try {
          const data = await getRoadmapsClient().copy(request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => {
        return [{ type: QueryTags.Roadmap, id: 'LIST' }]
      },
    }),
    updateRoadmap: builder.mutation<
      void,
      {
        request: UpdateRoadmapRequest
        cacheKey: number
      }
    >({
      queryFn: async (mutationRequest) => {
        try {
          const data = await getRoadmapsClient().update(
            mutationRequest.request.id,
            mutationRequest.request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => {
        return [
          { type: QueryTags.Roadmap, id: 'LIST' },
          { type: QueryTags.Roadmap, id: arg.cacheKey },
        ]
      },
    }),
    updateRoadmapColors: builder.mutation<
      void,
      {
        roadmapId: string
        request: UpdateRoadmapColorsRequest
        cacheKey: number
      }
    >({
      queryFn: async ({ roadmapId, request }) => {
        try {
          const data = await getRoadmapsClient().updateColors(roadmapId, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { cacheKey }) => [
        { type: QueryTags.Roadmap, id: 'LIST' },
        { type: QueryTags.Roadmap, id: cacheKey },
      ],
    }),
    deleteRoadmap: builder.mutation<void, string>({
      queryFn: async (roadmapId) => {
        try {
          const data = await getRoadmapsClient().delete(roadmapId)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => {
        return [{ type: QueryTags.Roadmap, id: 'LIST' }]
      },
    }),
    getRoadmapItems: builder.query<RoadmapItemListDto[], string>({
      queryFn: async (roadmapId: string) => {
        try {
          const data = await getRoadmapsClient().getItems(roadmapId)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.RoadmapItem, id: arg },
      ],
    }),
    getRoadmapItem: builder.query<
      RoadmapItemDetailsDto,
      { roadmapId: string; itemId: string }
    >({
      queryFn: async (request) => {
        try {
          const data = await getRoadmapsClient().getItem(
            request.roadmapId,
            request.itemId,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.RoadmapItem, id: arg.itemId },
      ],
    }),
    getRoadmapActivities: builder.query<RoadmapActivityListDto[], string>({
      queryFn: async (roadmapId: string) => {
        try {
          const data = await getRoadmapsClient().getActivities(roadmapId)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.RoadmapItem, id: arg },
      ],
    }),
    createRoadmapItem: builder.mutation<
      ObjectIdAndKey,
      | CreateRoadmapActivityRequest
      | CreateRoadmapMilestoneRequest
      | CreateRoadmapTimeboxRequest
    >({
      queryFn: async (request) => {
        try {
          const data = await getRoadmapsClient().createItem(
            request.roadmapId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => {
        return [{ type: QueryTags.RoadmapItem, id: arg.roadmapId }]
      },
    }),
    updateRoadmapItem: builder.mutation<
      void,
      | UpdateRoadmapActivityRequest
      | UpdateRoadmapMilestoneRequest
      | UpdateRoadmapTimeboxRequest
    >({
      queryFn: async (mutationRequest) => {
        try {
          const data = await getRoadmapsClient().updateItem(
            mutationRequest.roadmapId,
            mutationRequest.itemId,
            mutationRequest,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => {
        return [
          { type: QueryTags.RoadmapItem, id: arg.roadmapId },
          { type: QueryTags.RoadmapItem, id: arg.itemId },
        ]
      },
    }),
    patchRoadmapItem: builder.mutation<
      void,
      {
        roadmapId: string
        itemId: string
        patchOperations: Array<{
          op: 'replace' | 'add' | 'remove'
          path: string
          value?: any
        }>
      }
    >({
      queryFn: async ({ roadmapId, itemId, patchOperations }) => {
        try {
          const response = await authenticatedFetch(
            `/api/planning/roadmaps/${roadmapId}/items/${itemId}`,
            {
              method: 'PATCH',
              headers: {
                'Content-Type': 'application/json-patch+json',
              },
              body: JSON.stringify(patchOperations),
            },
          )

          if (!response.ok) {
            let errorData: unknown
            try {
              errorData = await response.json()
            } catch {
              errorData = {
                detail: await response.text(),
              }
            }

            return {
              error: {
                status: response.status,
                data: errorData,
              },
            }
          }

          return { data: null as unknown as void }
        } catch (error: any) {
          console.error('API Error:', error)
          return {
            error: {
              status: 'FETCH_ERROR',
              data: {
                detail:
                  error?.message ??
                  'An error occurred while updating the roadmap item.',
              },
            },
          }
        }
      },
      invalidatesTags: (result, error, arg) => {
        return [
          { type: QueryTags.RoadmapItem, id: arg.roadmapId },
          { type: QueryTags.RoadmapItem, id: arg.itemId },
        ]
      },
    }),
    updateRoadmapItemDates: builder.mutation<
      void,
      | UpdateRoadmapActivityDatesRequest
      | UpdateRoadmapMilestoneDatesRequest
      | UpdateRoadmapTimeboxDatesRequest
    >({
      queryFn: async (mutationRequest) => {
        try {
          const data = await getRoadmapsClient().updateItemDates(
            mutationRequest.roadmapId,
            mutationRequest.itemId,
            mutationRequest,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      // Patch the getRoadmapItems cache up front so consumers (e.g. the timeline)
      // see the new dates immediately, before the refetch lands. Otherwise the
      // dragged item visibly snaps back to its original position and then to the
      // new one once the refetch returns. Roll back on failure.
      onQueryStarted: async (arg, { dispatch, queryFulfilled }) => {
        const patchResult = dispatch(
          roadmapApi.util.updateQueryData(
            'getRoadmapItems',
            arg.roadmapId,
            (draft) => {
              applyOptimisticDates(draft, arg)
            },
          ),
        )
        try {
          await queryFulfilled
        } catch {
          patchResult.undo()
        }
      },
      invalidatesTags: (result, error, arg) => {
        return [
          { type: QueryTags.RoadmapItem, id: arg.roadmapId },
          { type: QueryTags.RoadmapItem, id: arg.itemId },
        ]
      },
    }),
    updateRoadmapActivityPlacement: builder.mutation<
      void,
      {
        request: UpdateRoadmapActivityPlacementRequest
      }
    >({
      queryFn: async (mutationRequest) => {
        try {
          const data = await getRoadmapsClient().updateActivityPlacement(
            mutationRequest.request.roadmapId,
            mutationRequest.request.itemId,
            mutationRequest.request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => {
        return [
          { type: QueryTags.RoadmapItem, id: arg.request.roadmapId },
          { type: QueryTags.RoadmapItem, id: arg.request.itemId },
        ]
      },
    }),
    deleteRoadmapItem: builder.mutation<
      void,
      {
        roadmapId: string
        itemId: string
      }
    >({
      queryFn: async (request) => {
        try {
          const data = await getRoadmapsClient().deleteItem(
            request.roadmapId,
            request.itemId,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => {
        return [
          { type: QueryTags.RoadmapItem, id: arg.roadmapId },
          { type: QueryTags.RoadmapItem, id: arg.itemId },
        ]
      },
    }),
    archiveRoadmap: builder.mutation<
      void,
      { id: string; cacheKey: number }
    >({
      queryFn: async ({ id }) => {
        try {
          const data = await getRoadmapsClient().archive(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { cacheKey }) => [
        { type: QueryTags.Roadmap, id: 'LIST' },
        { type: QueryTags.Roadmap, id: cacheKey },
      ],
    }),
    activateRoadmap: builder.mutation<
      void,
      { id: string; cacheKey: number }
    >({
      queryFn: async ({ id }) => {
        try {
          const data = await getRoadmapsClient().activate(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, { cacheKey }) => [
        { type: QueryTags.Roadmap, id: 'LIST' },
        { type: QueryTags.Roadmap, id: cacheKey },
      ],
    }),
    getRoadmapStateOptions: builder.query<OptionModel<number>[], void>({
      queryFn: async () => {
        try {
          const states = await getRoadmapsClient().getStateOptions()
          const data: OptionModel<number>[] = states
            .sort((a, b) => a.order - b.order)
            .map((s) => ({
              value: s.id,
              label: s.name,
            }))
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [QueryTags.RoadmapState],
    }),
    getVisibilityOptions: builder.query<OptionModel<number>[], void>({
      queryFn: async () => {
        try {
          const visibilities = await getRoadmapsClient().getVisibilityOptions()
          const data: OptionModel<number>[] = visibilities
            .sort((a, b) => a.order - b.order)
            .map((s) => ({
              value: s.id,
              label: s.name,
            }))
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [QueryTags.RoadmapVisibility],
    }),
  }),
})

export const {
  useGetRoadmapsQuery,
  useGetRoadmapQuery,
  useCreateRoadmapMutation,
  useCopyRoadmapMutation,
  useUpdateRoadmapMutation,
  useUpdateRoadmapColorsMutation,
  useDeleteRoadmapMutation,
  useArchiveRoadmapMutation,
  useActivateRoadmapMutation,
  useGetRoadmapItemsQuery,
  useGetRoadmapItemQuery,
  useGetRoadmapActivitiesQuery,
  useCreateRoadmapItemMutation,
  useUpdateRoadmapItemMutation,
  usePatchRoadmapItemMutation,
  useUpdateRoadmapItemDatesMutation,
  useUpdateRoadmapActivityPlacementMutation,
  useDeleteRoadmapItemMutation,
  useGetRoadmapStateOptionsQuery,
  useGetVisibilityOptionsQuery,
} = roadmapApi

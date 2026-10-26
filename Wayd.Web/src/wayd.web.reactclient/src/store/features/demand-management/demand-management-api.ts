import {
  getIntakeChannelsClient,
  getIntakeDesksClient,
  getIntakeEmployeeOptionsClient,
  getIntakeRequestsClient,
} from '@/src/services/clients'
import {
  CreateIntakeDeskRequest,
  CreateIntakeRequestRequest,
  EmployeeNavigationDto,
  IntakeChannelDto,
  IntakeDeskDto,
  IntakeDeskRoleDto,
  IntakeDeskVisibilityDto,
  IntakeRequestDetailsDto,
  IntakeRequestSummaryDto,
  NeedByBasisDto,
  NeedByBasisFamilyDto,
  ObjectIdAndKey,
  PagedResponseOfActivityLogDto,
  RerouteIntakeRequestRequest,
  UpdateIntakeDeskRequest,
  UpdateIntakeRequestRequest,
} from '@/src/services/wayd-api'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import { ActivityLogQueryArg } from '@/src/components/common/activities'

export interface IntakeRequestListArgs {
  deskId?: string
  /** Only requests the caller requested or submitted. */
  mine?: boolean
}

export interface IntakeRequestParticipantArgs {
  requestId: string
  employeeId: string
}

/**
 * A request's details are fetched by the key in the URL, and its mutations know only its id, so every
 * entry carries both tags. The activity log is keyed by id, which is what the request page passes.
 */
const requestTags = (requestId: string) => [
  { type: QueryTags.IntakeRequest, id: requestId },
  { type: QueryTags.ActivityLog, id: requestId },
]

const listTag = { type: QueryTags.IntakeRequest, id: 'LIST' } as const

const deskListTag = { type: QueryTags.IntakeDesk, id: 'LIST' } as const

export interface RerouteIntakeRequestArgs extends RerouteIntakeRequestRequest {
  requestId: string
}

/**
 * Rejects with what the generated client threw, unchanged, so pages and forms read it with `toApiError`:
 * a documented response arrives as its problem details and an undocumented one as an `ApiException`.
 */
const toError = (error: unknown) => {
  console.error('API Error:', error)
  return { error }
}

export const demandManagementApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getIntakeRequests: builder.query<
      IntakeRequestSummaryDto[],
      IntakeRequestListArgs | void
    >({
      queryFn: async (args) => {
        try {
          const data = await getIntakeRequestsClient().getIntakeRequests(
            args?.deskId,
            args?.mine,
          )
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      providesTags: (result = []) => [
        listTag,
        ...result.map(({ id }) => ({ type: QueryTags.IntakeRequest, id })),
      ],
    }),

    getIntakeRequest: builder.query<IntakeRequestDetailsDto, string>({
      queryFn: async (idOrKey) => {
        try {
          const data = await getIntakeRequestsClient().getIntakeRequest(idOrKey)
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      providesTags: (result, _error, idOrKey) => [
        ...(result
          ? [{ type: QueryTags.IntakeRequest, id: result.summary.id }]
          : []),
        { type: QueryTags.IntakeRequest, id: idOrKey },
      ],
    }),

    // Takes the shared activity-log argument so the request page can drive it with useActivityLog. The
    // request page passes the id, which is what mutations invalidate by.
    getIntakeRequestActivities: builder.query<
      PagedResponseOfActivityLogDto,
      ActivityLogQueryArg
    >({
      queryFn: async ({ idOrKey, page, pageSize }) => {
        try {
          const data = await getIntakeRequestsClient().getActivities(
            String(idOrKey),
            page,
            pageSize,
          )
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      providesTags: (_result, _error, { idOrKey }) => [
        { type: QueryTags.ActivityLog, id: String(idOrKey) },
      ],
    }),

    createIntakeRequest: builder.mutation<
      ObjectIdAndKey,
      CreateIntakeRequestRequest
    >({
      queryFn: async (request) => {
        try {
          const data = await getIntakeRequestsClient().create(request)
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      // A new request changes its desk's waiting count and whether the desk can be deleted.
      invalidatesTags: [listTag, QueryTags.IntakeDesk],
    }),

    updateIntakeRequest: builder.mutation<void, UpdateIntakeRequestRequest>({
      queryFn: async (request) => {
        try {
          const data = await getIntakeRequestsClient().update(
            request.id,
            request,
          )
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      invalidatesTags: (_result, _error, { id }) => [
        ...requestTags(id),
        listTag,
      ],
    }),

    addIntakeRequestParticipant: builder.mutation<
      void,
      IntakeRequestParticipantArgs
    >({
      queryFn: async ({ requestId, employeeId }) => {
        try {
          const data = await getIntakeRequestsClient().addParticipant(
            requestId,
            { employeeId },
          )
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      invalidatesTags: (_result, _error, { requestId }) =>
        requestTags(requestId),
    }),

    // A move changes the desk on the request and the waiting counts on both desks.
    rerouteIntakeRequest: builder.mutation<void, RerouteIntakeRequestArgs>({
      queryFn: async ({ requestId, ...request }) => {
        try {
          const data = await getIntakeRequestsClient().reroute(
            requestId,
            request,
          )
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      invalidatesTags: (_result, _error, { requestId }) => [
        ...requestTags(requestId),
        listTag,
        QueryTags.IntakeDesk,
      ],
    }),

    removeIntakeRequestParticipant: builder.mutation<
      void,
      IntakeRequestParticipantArgs
    >({
      queryFn: async ({ requestId, employeeId }) => {
        try {
          const data = await getIntakeRequestsClient().removeParticipant(
            requestId,
            employeeId,
          )
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      invalidatesTags: (_result, _error, { requestId }) =>
        requestTags(requestId),
    }),

    getNeedByBases: builder.query<NeedByBasisDto[], void>({
      queryFn: async () => {
        try {
          const data = await getIntakeRequestsClient().getNeedByBases()
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      providesTags: [{ type: QueryTags.IntakeLookup, id: 'NEED-BY-BASES' }],
    }),

    getNeedByBasisFamilies: builder.query<NeedByBasisFamilyDto[], void>({
      queryFn: async () => {
        try {
          const data = await getIntakeRequestsClient().getNeedByBasisFamilies()
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      providesTags: [{ type: QueryTags.IntakeLookup, id: 'NEED-BY-FAMILIES' }],
    }),

    getIntakeDesks: builder.query<
      IntakeDeskDto[],
      { submittableOnly?: boolean } | void
    >({
      queryFn: async (args) => {
        try {
          const data = await getIntakeDesksClient().getIntakeDesks(
            args?.submittableOnly,
          )
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      providesTags: (result = []) => [
        deskListTag,
        ...result.map(({ id }) => ({ type: QueryTags.IntakeDesk, id })),
      ],
    }),

    // Fetched by the key in the URL; mutations know only the id, so the entry carries both tags.
    getIntakeDesk: builder.query<IntakeDeskDto, string>({
      queryFn: async (idOrKey) => {
        try {
          const data = await getIntakeDesksClient().getIntakeDesk(idOrKey)
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      providesTags: (result, _error, idOrKey) => [
        ...(result ? [{ type: QueryTags.IntakeDesk, id: result.id }] : []),
        { type: QueryTags.IntakeDesk, id: idOrKey },
      ],
    }),

    updateIntakeDesk: builder.mutation<void, UpdateIntakeDeskRequest>({
      queryFn: async (request) => {
        try {
          const data = await getIntakeDesksClient().update(request.id, request)
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      // Removing a member unassigns them from the desk's requests, and a rename shows on every request
      // on the desk, so every cached request refreshes too.
      invalidatesTags: (_result, _error, { id }) => [
        { type: QueryTags.IntakeDesk, id },
        deskListTag,
        QueryTags.IntakeRequest,
      ],
    }),

    deleteIntakeDesk: builder.mutation<void, string>({
      queryFn: async (id) => {
        try {
          const data = await getIntakeDesksClient().delete(id)
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      invalidatesTags: [deskListTag],
    }),

    getIntakeDeskRoles: builder.query<IntakeDeskRoleDto[], void>({
      queryFn: async () => {
        try {
          const data = await getIntakeDesksClient().getRoles()
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      providesTags: [{ type: QueryTags.IntakeLookup, id: 'DESK-ROLES' }],
    }),

    createIntakeDesk: builder.mutation<ObjectIdAndKey, CreateIntakeDeskRequest>(
      {
        queryFn: async (request) => {
          try {
            const data = await getIntakeDesksClient().create(request)
            return { data }
          } catch (error) {
            return toError(error)
          }
        },
        invalidatesTags: [deskListTag],
      },
    ),

    getIntakeDeskVisibilities: builder.query<IntakeDeskVisibilityDto[], void>({
      queryFn: async () => {
        try {
          const data = await getIntakeDesksClient().getVisibilities()
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      providesTags: [{ type: QueryTags.IntakeLookup, id: 'VISIBILITIES' }],
    }),

    getIntakeChannels: builder.query<
      IntakeChannelDto[],
      { includeArchived?: boolean } | void
    >({
      queryFn: async (args) => {
        try {
          const data = await getIntakeChannelsClient().getIntakeChannels(
            args?.includeArchived,
          )
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      providesTags: [{ type: QueryTags.IntakeChannel, id: 'LIST' }],
    }),

    getIntakeEmployeeOptions: builder.query<EmployeeNavigationDto[], void>({
      queryFn: async () => {
        try {
          const data =
            await getIntakeEmployeeOptionsClient().getEmployeeOptions()
          return { data }
        } catch (error) {
          return toError(error)
        }
      },
      providesTags: [{ type: QueryTags.IntakeEmployeeOption, id: 'LIST' }],
    }),
  }),
})

export const {
  useGetIntakeRequestsQuery,
  useGetIntakeRequestQuery,
  useGetIntakeRequestActivitiesQuery,
  useLazyGetIntakeRequestActivitiesQuery,
  useCreateIntakeRequestMutation,
  useUpdateIntakeRequestMutation,
  useAddIntakeRequestParticipantMutation,
  useRemoveIntakeRequestParticipantMutation,
  useGetNeedByBasesQuery,
  useGetNeedByBasisFamiliesQuery,
  useGetIntakeDesksQuery,
  useGetIntakeDeskQuery,
  useGetIntakeDeskVisibilitiesQuery,
  useGetIntakeDeskRolesQuery,
  useCreateIntakeDeskMutation,
  useUpdateIntakeDeskMutation,
  useDeleteIntakeDeskMutation,
  useRerouteIntakeRequestMutation,
  useGetIntakeChannelsQuery,
  useGetIntakeEmployeeOptionsQuery,
} = demandManagementApi

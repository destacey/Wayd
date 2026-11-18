import {
  linksCreate,
  linksDelete,
  linksGetLinks,
  linksUpdate,
} from '@/src/services/api/links'
import { LinkRequest, RecordLinksDto } from '@/src/services/api/wayd.schemas'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'

/**
 * The kinds of record that can carry links, as the API names them. Each is
 * registered on the server with the rules for who may read and change them.
 */
export const LinkOwnerTypes = {
  Team: 'organization.team',
  PlanningInterval: 'planning.planning-interval',
  PlanningIntervalObjective: 'planning.planning-interval-objective',
  Risk: 'planning.risk',
  Portfolio: 'ppm.portfolio',
  Program: 'ppm.program',
  Project: 'ppm.project',
  StrategicInitiative: 'ppm.strategic-initiative',
  Incident: 'product.incident',
  Sprint: 'work.sprint',
} as const

export type LinkOwnerType = (typeof LinkOwnerTypes)[keyof typeof LinkOwnerTypes]

/** The record a set of links belongs to. */
export interface LinkOwner {
  ownerType: LinkOwnerType
  ownerId: string
}

/** One link on a record. */
export interface LinkTarget extends LinkOwner {
  linkId: string
}

const ownerTag = ({ ownerType, ownerId }: LinkOwner) => ({
  type: QueryTags.Links,
  id: `${ownerType}:${ownerId}`,
})

/**
 * What changing a record's links refreshes: the links, the record's Activity,
 * where every link event is filed, and an incident's timeline, which records
 * each change as an entry.
 */
const ownerWriteTags = (owner: LinkOwner) => [
  ownerTag(owner),
  { type: QueryTags.ActivityLog, id: owner.ownerId },
  ...(owner.ownerType === LinkOwnerTypes.Incident
    ? [{ type: QueryTags.IncidentTimelineLinkEntries, id: owner.ownerId }]
    : []),
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

export const linksApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getLinks: builder.query<RecordLinksDto, LinkOwner>({
      queryFn: ({ ownerType, ownerId }) =>
        run(() => linksGetLinks(ownerType, ownerId)),
      providesTags: (result, error, args) => [ownerTag(args)],
    }),

    createLink: builder.mutation<string, LinkOwner & { request: LinkRequest }>({
      queryFn: ({ ownerType, ownerId, request }) =>
        run(() => linksCreate(ownerType, ownerId, request)),
      invalidatesTags: (result, error, args) =>
        error ? [] : ownerWriteTags(args),
    }),

    updateLink: builder.mutation<null, LinkTarget & { request: LinkRequest }>({
      queryFn: ({ ownerType, ownerId, linkId, request }) =>
        runVoid(() => linksUpdate(ownerType, ownerId, linkId, request)),
      invalidatesTags: (result, error, args) =>
        error ? [] : ownerWriteTags(args),
    }),

    deleteLink: builder.mutation<null, LinkTarget>({
      queryFn: ({ ownerType, ownerId, linkId }) =>
        runVoid(() => linksDelete(ownerType, ownerId, linkId)),
      invalidatesTags: (result, error, args) =>
        error ? [] : ownerWriteTags(args),
    }),
  }),
})

export const {
  useGetLinksQuery,
  useCreateLinkMutation,
  useUpdateLinkMutation,
  useDeleteLinkMutation,
} = linksApi

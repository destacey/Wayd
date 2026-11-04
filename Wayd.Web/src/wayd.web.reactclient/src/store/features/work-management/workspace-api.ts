import { getWorkspacesClient } from '@/src/services/clients'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import {
  ScopedDependencyDto,
  SetExternalUrlTemplatesRequest,
  UpdateWorkItemProjectRequest,
  WorkItemDetailsDto,
  WorkItemListDto,
  WorkItemProgressDailyRollupDto,
  WorkItemProjectInfoDto,
  WorkspaceDto,
  WorkspaceListDto,
  WorkTypeTier,
} from '@/src/services/wayd-api'

/**
 * What changes when a work item's project does: the project lists, the work items and their children,
 * searches that show each item's project, and forecasts built from a project's work items.
 */
export const projectLinkTags = [
  QueryTags.ProjectWorkItems,
  QueryTags.WorkItem,
  QueryTags.WorkItemChildren,
  QueryTags.WorkItemSearch,
  QueryTags.Forecast,
]

/** A work item search narrowed to one tier; a bare string searches every tier. */
export interface SearchWorkItemsArgs {
  query: string
  tier?: WorkTypeTier
}

export interface GetWorkItemRequest {
  idOrKey: string
  workItemKey: string
}

export interface GetChildWorkItemsRequest {
  idOrKey: string
  workItemKey: string
}

export interface GetMetricsRequest {
  idOrKey: string
  workItemKey: string
}

export interface SetWorkspaceExternalUrlTemplatesRequest {
  workspaceId: string
  externalUrlTemplatesRequest: SetExternalUrlTemplatesRequest
}

export const workspaceApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getWorkspaces: builder.query<WorkspaceListDto[], boolean>({
      queryFn: async (includeInactive) => {
        try {
          const data = await getWorkspacesClient().getList(includeInactive)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result) => [
        QueryTags.Workspace,
        ...(result?.map(({ key }) => ({ type: QueryTags.Workspace, key })) ?? []),
      ],
    }),
    getWorkspace: builder.query<WorkspaceDto, string>({
      queryFn: async (idOrKey: string) => {
        try {
          const data = await getWorkspacesClient().get(idOrKey)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.Workspace, id: arg }, // typically arg is the key
      ],
    }),
    setWorkspaceExternalUrlTemplates: builder.mutation<
      void,
      SetWorkspaceExternalUrlTemplatesRequest
    >({
      queryFn: async (request) => {
        try {
          const data = await getWorkspacesClient().setExternalUrlTemplates(
            request.workspaceId,
            request.externalUrlTemplatesRequest,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.Workspace, id: arg.workspaceId },
      ],
    }),
    getWorkItems: builder.query<WorkItemListDto[], string>({
      queryFn: async (idOrKey: string) => {
        try {
          const data = await getWorkspacesClient().getWorkItems(idOrKey)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result) => [
        QueryTags.WorkItem,
        ...(result?.map(({ key }) => ({ type: QueryTags.WorkItem, key })) ?? []),
      ],
    }),
    getWorkItem: builder.query<WorkItemDetailsDto, GetWorkItemRequest>({
      queryFn: async (request: GetWorkItemRequest) => {
        try {
          const data = await getWorkspacesClient().getWorkItem(
            request.idOrKey,
            request.workItemKey,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.WorkItem, id: arg.workItemKey }, // typically arg is the key
      ],
    }),
    getWorkItemProjectInfo: builder.query<
      WorkItemProjectInfoDto,
      GetWorkItemRequest
    >({
      queryFn: async (request: GetWorkItemRequest) => {
        try {
          const data = await getWorkspacesClient().getWorkItemProjectInfo(
            request.idOrKey,
            request.workItemKey,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.WorkItem, id: arg.workItemKey }, // typically arg is the key
      ],
    }),
    updateWorkItemProject: builder.mutation<
      void,
      {
        workspaceId: string
        request: UpdateWorkItemProjectRequest
        cacheKey: string
      }
    >({
      queryFn: async ({ workspaceId, request }) => {
        try {
          const data = await getWorkspacesClient().updateWorkItemProject(
            workspaceId,
            request.workItemId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        // TODO: there are a few more tags that need to be invalidated here
        { type: QueryTags.WorkItem, id: arg.cacheKey },
        { type: QueryTags.WorkItemChildren, id: arg.cacheKey },
        // Both the project it left and the one it joined, which the request does not name.
        ...projectLinkTags,
      ],
    }),
    getChildWorkItems: builder.query<
      WorkItemListDto[],
      GetChildWorkItemsRequest
    >({
      queryFn: async (request: GetChildWorkItemsRequest) => {
        try {
          const data = await getWorkspacesClient().getChildWorkItems(
            request.idOrKey,
            request.workItemKey,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result) => [
        QueryTags.WorkItemChildren,
        ...(result?.map(({ key }) => ({ type: QueryTags.WorkItemChildren, key })) ?? []),
      ],
    }),
    getWorkItemDependencies: builder.query<
      ScopedDependencyDto[],
      { workspaceIdOrKey: string; workItemKey: string }
    >({
      queryFn: async ({ workspaceIdOrKey, workItemKey }) => {
        try {
          const data = await getWorkspacesClient().getWorkItemDependencies(
            workspaceIdOrKey,
            workItemKey,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, { workItemKey }) => [
        QueryTags.WorkItemDependency,
        // Use workItemKey instead of id for cache invalidation
        ...(result?.map(() => ({
          type: QueryTags.WorkItemDependency,
          id: workItemKey,
        })) ?? []),
      ],
    }),
    getWorkItemMetrics: builder.query<
      WorkItemProgressDailyRollupDto[],
      GetMetricsRequest
    >({
      queryFn: async (request: GetMetricsRequest) => {
        try {
          const data = await getWorkspacesClient().getMetrics(
            request.idOrKey,
            request.workItemKey,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.WorkItemMetrics, id: arg.workItemKey },
      ],
    }),
    searchWorkItems: builder.query<
      WorkItemListDto[],
      string | SearchWorkItemsArgs
    >({
      queryFn: async (arg) => {
        const { query, tier } =
          typeof arg === 'string' ? { query: arg, tier: undefined } : arg
        try {
          const data = await getWorkspacesClient().searchWorkItems(
            query,
            50,
            tier,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result) => [
        QueryTags.WorkItemSearch,
        ...(result?.map(({ key }) => ({ type: QueryTags.WorkItemSearch, key })) ?? []),
      ],
    }),
  }),
})

export const {
  useGetWorkspacesQuery,
  useGetWorkspaceQuery,
  useGetWorkItemsQuery,
  useGetWorkItemQuery,
  useGetWorkItemProjectInfoQuery,
  useUpdateWorkItemProjectMutation,
  useGetChildWorkItemsQuery,
  useGetWorkItemDependenciesQuery,
  useGetWorkItemMetricsQuery,
  useSearchWorkItemsQuery,
  useSetWorkspaceExternalUrlTemplatesMutation,
} = workspaceApi

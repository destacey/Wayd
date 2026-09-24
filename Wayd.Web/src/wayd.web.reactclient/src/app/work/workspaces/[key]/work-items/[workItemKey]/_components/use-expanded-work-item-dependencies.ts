'use client'

import { useAppDispatch, useAppSelector } from '@/src/hooks'
import { ScopedDependencyDto } from '@/src/services/wayd-api'
import { workspaceApi } from '@/src/store/features/work-management/workspace-api'
import { useEffect } from 'react'
import { shallowEqual } from 'react-redux'
import type { RootState } from '@/src/store'
import type {
  WorkItemDependencyExpansion,
  WorkItemMapRef,
} from './work-item-dependency-neighbourhood'

// The same argument shape the Dependencies section and the Overview use, so a work item that is both
// expanded here and opened there is fetched once.
const argsFor = (item: WorkItemMapRef) => ({
  workspaceIdOrKey: item.workspaceKey,
  workItemKey: item.key,
})

type ExpansionSelector = ReturnType<
  typeof workspaceApi.endpoints.getWorkItemDependencies.select
>

// One selector per work item, kept: an endpoint's select() builds a new memoized selector on every call,
// and a fresh one returns a fresh object, which would re-render the map on every change to the store.
const selectors = new Map<string, ExpansionSelector>()
const selectorFor = (item: WorkItemMapRef) => {
  const cacheKey = `${item.workspaceKey}/${item.key}`
  let selector = selectors.get(cacheKey)
  if (!selector) {
    selector = workspaceApi.endpoints.getWorkItemDependencies.select(
      argsFor(item),
    )
    selectors.set(cacheKey, selector)
  }
  return selector
}

type Result = ReturnType<ExpansionSelector>

/**
 * Finds every expanded item that can be fetched yet, and what the store holds for each.
 *
 * An expansion is stored by id alone, but fetching one takes the item's workspace and key, which only a
 * loaded dependency list carries. An item two hops out is known once the one before it has loaded, so the
 * ids are resolved in rounds, and one still unknown is simply fetched on a later render.
 */
const resolve = (
  state: RootState,
  subjectDependencies: ScopedDependencyDto[] | undefined,
  expandedIds: string[],
): (WorkItemMapRef | Result)[] => {
  const known = new Map<string, WorkItemMapRef>()
  const learn = (dependencies: ScopedDependencyDto[] | undefined) =>
    dependencies?.forEach(({ dependency }) =>
      known.set(dependency.id, dependency),
    )
  learn(subjectDependencies)

  const found = new Map<string, [WorkItemMapRef, Result]>()
  let progressed = true
  while (progressed) {
    progressed = false
    for (const id of expandedIds) {
      const item = known.get(id)
      if (found.has(id) || !item) continue

      const result = selectorFor(item)(state)
      found.set(id, [item, result])
      learn(result.data)
      progressed = true
    }
  }

  // Flattened into references the store keeps stable, so a shallow comparison can tell when nothing moved.
  return expandedIds.flatMap((id) => found.get(id) ?? [])
}

/**
 * The dependencies of every work item expanded on the map, by id.
 *
 * Subscribed through the endpoint rather than a hook per item, because how many items are expanded changes
 * as the reader clicks and hooks cannot be called a varying number of times. The subscriptions share the
 * Dependencies section's cache tag, so a refetch there refreshes the expansions too.
 */
export const useExpandedWorkItemDependencies = (
  subjectDependencies: ScopedDependencyDto[] | undefined,
  expandedIds: string[],
): Record<string, Omit<WorkItemDependencyExpansion, 'showAll'>> => {
  const dispatch = useAppDispatch()

  const flat = useAppSelector(
    (state) => resolve(state, subjectDependencies, expandedIds),
    shallowEqual,
  )

  const entries: [string, Omit<WorkItemDependencyExpansion, 'showAll'>][] = []
  for (let i = 0; i < flat.length; i += 2) {
    const item = flat[i] as WorkItemMapRef
    const result = flat[i + 1] as Result
    entries.push([
      item.id,
      { workItem: item, dependencies: result.data, isError: result.isError },
    ])
  }

  const subscribed = entries
    .map(([, { workItem }]) => `${workItem.workspaceKey}/${workItem.key}`)
    .sort()
    .join('|')

  useEffect(() => {
    if (!subscribed) return

    const subscriptions = subscribed.split('|').map((cacheKey) => {
      const [workspaceKey, key] = cacheKey.split('/')
      return dispatch(
        workspaceApi.endpoints.getWorkItemDependencies.initiate({
          workspaceIdOrKey: workspaceKey,
          workItemKey: key,
        }),
      )
    })

    return () => subscriptions.forEach((s) => s.unsubscribe())
  }, [dispatch, subscribed])

  return Object.fromEntries(entries)
}

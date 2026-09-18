'use client'

import { useAppDispatch, useAppSelector } from '@/src/hooks'
import { productsApi } from '@/src/store/features/product-management/products-api'
import { shallowEqual } from 'react-redux'
import { useEffect } from 'react'
import type { DependencyExpansion } from './dependency-neighbourhood'

const argsFor = (productId: string) => ({
  idOrKey: productId,
  includeEnded: false,
})

type ExpansionSelector = ReturnType<
  typeof productsApi.endpoints.getProductDependencies.select
>

// One selector per product, kept: an endpoint's select() builds a new memoized selector on every call,
// and a fresh one returns a fresh object, which would re-render the map on every change to the store.
const selectors = new Map<string, ExpansionSelector>()
const selectorFor = (productId: string) => {
  let selector = selectors.get(productId)
  if (!selector) {
    selector = productsApi.endpoints.getProductDependencies.select(
      argsFor(productId),
    )
    selectors.set(productId, selector)
  }
  return selector
}

/**
 * The dependencies of every product expanded on the map, by product id.
 *
 * Subscribed through the endpoint rather than a hook per product, because how many products are expanded
 * changes as the reader clicks and hooks cannot be called a varying number of times. The subscriptions
 * share the Dependencies section's cache tag, so adding or ending a link refreshes the expansions too.
 */
export const useExpandedProductDependencies = (
  productIds: string[],
): Record<string, Pick<DependencyExpansion, 'dependencies' | 'isError'>> => {
  const dispatch = useAppDispatch()
  const subscribed = [...new Set(productIds)].sort().join('|')

  useEffect(() => {
    if (!subscribed) return

    const subscriptions = subscribed
      .split('|')
      .map((id) =>
        dispatch(
          productsApi.endpoints.getProductDependencies.initiate(argsFor(id)),
        ),
      )

    return () => subscriptions.forEach((s) => s.unsubscribe())
  }, [dispatch, subscribed])

  const results = useAppSelector(
    (state) => productIds.map((id) => selectorFor(id)(state)),
    shallowEqual,
  )

  return Object.fromEntries(
    productIds.map((id, index) => [
      id,
      { dependencies: results[index].data, isError: results[index].isError },
    ]),
  )
}

import {
  buildDependencyNeighbourhood,
  type DependencyExpansion,
  type DependencyFarSide,
  type DependencyMapLink,
  type DependencyMapLinks,
  type DependencyMapRecord,
  type DependencyNeighbourhood,
} from '@/src/components/common/dependency-map'
import {
  DependencyStrength,
  NavigationDto,
  ProductDependenciesDto,
  ProductDependencyDto,
} from '@/src/services/wayd-api'

/** Which links the map draws: all of them, or only those a product cannot work without. */
export type DependencyStrengthFilter = 'all' | 'hard'

/** A product the reader expanded, with whatever has loaded for it. */
export interface ProductDependencyExpansion extends Omit<
  DependencyExpansion,
  'links'
> {
  /** Undefined while loading. */
  dependencies?: ProductDependenciesDto
}

export type ProductDependencyExpansions = Record<
  DependencyFarSide,
  Record<string, ProductDependencyExpansion>
>

export interface BuildProductDependencyNeighbourhoodOptions {
  productId: string
  productName: string
  productKey: number
  dependencies: ProductDependenciesDto | undefined
  /** Products drawn per column, per product whose links fill it, before the rest collapse into a count. */
  maxPerSide?: number
  strengthFilter?: DependencyStrengthFilter
  expansions?: ProductDependencyExpansions
  /** Sides where the subject's own overflow has been revealed. */
  subjectShowAll?: DependencyFarSide[]
}

export const productHref = (productKey: number) =>
  `/product-management/products/${productKey}`

const record = (product: NavigationDto): DependencyMapRecord => ({
  id: product.id,
  label: product.name,
  href: productHref(product.key),
})

/**
 * A link as the map draws it: the arrow runs from the product that relies on the other, so what a product
 * relies on sits on its right and what relies on it on its left.
 */
const toLink = (link: ProductDependencyDto): DependencyMapLink => ({
  id: link.id,
  source: record(link.product),
  target: record(link.dependsOnProduct),
  // Defaulted because a cached response written before the paths existed would otherwise take the whole
  // section down rather than drawing a flatter map.
  sourcePath: (link.productPath ?? []).map(record),
  targetPath: (link.dependsOnProductPath ?? []).map(record),
  variant: link.strength,
})

/**
 * The map of what a product relies on and what relies on it.
 *
 * Ended links are left out whatever the caller passed: a map reads as what is true now, and a link that
 * has stopped says nothing about today's blast radius. The Dependencies section is where history is read.
 * Filtered before anything is grouped or capped: products and boxes exist only because a link put them
 * there, so one left with no links is never drawn, and soft links cannot take the slots the cap would
 * otherwise leave for hard ones.
 */
export const buildProductDependencyNeighbourhood = ({
  productId,
  productName,
  productKey,
  dependencies,
  maxPerSide,
  strengthFilter = 'all',
  expansions,
  subjectShowAll,
}: BuildProductDependencyNeighbourhoodOptions): DependencyNeighbourhood => {
  const links = (
    deps: ProductDependenciesDto | undefined,
  ): DependencyMapLinks | undefined => {
    if (!deps) return undefined

    const open = (list: ProductDependencyDto[]) =>
      list
        .filter(
          (d) =>
            !d.endsOn &&
            (strengthFilter === 'all' ||
              d.strength === DependencyStrength.Hard),
        )
        .map(toLink)

    return { left: open(deps.usedBy), right: open(deps.dependsOn) }
  }

  const bySide = (side: DependencyFarSide) =>
    Object.fromEntries(
      Object.entries(expansions?.[side] ?? {}).map(
        ([id, { dependencies: deps, ...rest }]) => [
          id,
          { ...rest, links: links(deps) },
        ],
      ),
    )

  return buildDependencyNeighbourhood({
    subject: {
      id: productId,
      label: productName,
      href: productHref(productKey),
    },
    links: links(dependencies),
    maxPerSide,
    overflowQualifier: strengthFilter === 'hard' ? 'hard' : undefined,
    expansions: { left: bySide('left'), right: bySide('right') },
    subjectShowAll,
  })
}

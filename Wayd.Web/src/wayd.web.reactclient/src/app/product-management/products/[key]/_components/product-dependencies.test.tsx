import {
  DependencyStrength,
  InteractionStyle,
  ProductDependencyDto,
} from '@/src/services/wayd-api'
import { SET_FILTER_BLANK } from '@/src/components/common/wayd-grid-core'
import { buildDependencyColumns } from './product-dependencies'

// The actions hook pulls in the dialogs; the columns under test only take its item builder.
jest.mock('./use-product-dependency-actions', () => ({
  __esModule: true,
  default: () => ({ getActionItems: () => [], dialogs: null }),
}))

const storefront = { id: 'storefront', key: 1, name: 'Storefront' }
const web = { id: 'web', key: 2, name: 'Storefront Web' }
const identity = { id: 'identity', key: 3, name: 'Identity Service' }

const link = (
  overrides: Partial<ProductDependencyDto> = {},
): ProductDependencyDto =>
  ({
    id: 'dependency-1',
    product: web,
    dependsOnProduct: identity,
    strength: DependencyStrength.Hard,
    startsOn: new Date('2026-03-01T00:00:00Z'),
    ...overrides,
  }) as ProductDependencyDto

const column = (
  columns: ReturnType<typeof buildDependencyColumns>,
  id: string,
) =>
  columns.find((c) => c.id === id) as {
    header: string
    accessorFn: (row: ProductDependencyDto) => string
  }

describe('buildDependencyColumns', () => {
  it('leads with the far end of the link in each direction', () => {
    // Arrange
    const dependsOn = buildDependencyColumns('dependsOn', web.id, () => [])
    const usedBy = buildDependencyColumns('usedBy', identity.id, () => [])

    // Act / Assert
    expect(column(dependsOn, 'farEnd').header).toBe('Depends On')
    expect(column(dependsOn, 'farEnd').accessorFn(link())).toBe(
      'Identity Service',
    )

    expect(column(usedBy, 'farEnd').header).toBe('Used By')
    expect(column(usedBy, 'farEnd').accessorFn(link())).toBe('Storefront Web')
  })

  it('leaves the near end empty on a link of the page product itself', () => {
    // Arrange
    const columns = buildDependencyColumns('dependsOn', web.id, () => [])

    // Act / Assert
    expect(column(columns, 'nearEnd').accessorFn(link())).toBe('')
  })

  it('names the descendant a rolled-up link starts from', () => {
    // Arrange — Storefront's page, showing a link recorded on Storefront Web
    const columns = buildDependencyColumns('dependsOn', storefront.id, () => [])

    // Act / Assert
    expect(column(columns, 'nearEnd').header).toBe('From')
    expect(column(columns, 'nearEnd').accessorFn(link())).toBe('Storefront Web')
  })

  it('names the descendant a rolled-up link lands on', () => {
    // Arrange — a platform's page, showing a link to one of its services
    const columns = buildDependencyColumns('usedBy', 'platform', () => [])

    // Act / Assert
    expect(column(columns, 'nearEnd').header).toBe('Via')
    expect(column(columns, 'nearEnd').accessorFn(link())).toBe(
      'Identity Service',
    )
  })

  describe('the Interaction column', () => {
    const interaction = (styles?: InteractionStyle[]) => {
      const columns = buildDependencyColumns('dependsOn', web.id, () => [])
      const col = columns.find((c) => c.id === 'interaction') as unknown as {
        accessorFn: (row: ProductDependencyDto) => string
        filterFn: (
          row: { original: ProductDependencyDto; getValue: () => unknown },
          columnId: string,
          filterValue: unknown,
        ) => boolean
      }
      const row = link({ interactionStyles: styles })
      return {
        value: col.accessorFn(row),
        matches: (selected: string[]) =>
          col.filterFn(
            { original: row, getValue: () => col.accessorFn(row) },
            'interaction',
            { type: 'set', values: selected },
          ),
      }
    }

    it('has no value of its own where no styles were recorded', () => {
      // Arrange / Act — the grid offers its own blank option for these, so inventing a pseudo-style here
      // would duplicate the cell's wording and make this column behave unlike its neighbours
      const { value } = interaction(undefined)

      // Assert
      expect(value).toBe('')
    })

    it('matches a dependency that carries any one selected style', () => {
      // Arrange — the point of filtering on individual styles rather than the joined pair
      const both = interaction([
        InteractionStyle.Synchronous,
        InteractionStyle.Asynchronous,
      ])

      // Act / Assert
      expect(both.matches(['Asynchronous'])).toBe(true)
      expect(both.matches(['Synchronous'])).toBe(true)
    })

    it('does not match a style the dependency does not carry', () => {
      // Arrange / Act
      const syncOnly = interaction([InteractionStyle.Synchronous])

      // Assert
      expect(syncOnly.matches(['Asynchronous'])).toBe(false)
    })

    it('filters the unrecorded ones through the blank option the grid adds', () => {
      // Arrange / Act
      const none = interaction(undefined)

      // Assert
      expect(none.matches([SET_FILTER_BLANK])).toBe(true)
      expect(none.matches(['Synchronous'])).toBe(false)
    })
  })
})

import {
  DependencyStrength,
  ProductDependencyDto,
} from '@/src/services/wayd-api'
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
})

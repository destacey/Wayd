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

const trio = { id: 'trio', key: 1, name: 'Trio' }
const vms = { id: 'vms', key: 2, name: 'Trio VMS' }
const identity = { id: 'identity', key: 3, name: 'Argo Identity' }

const link = (
  overrides: Partial<ProductDependencyDto> = {},
): ProductDependencyDto =>
  ({
    id: 'dependency-1',
    product: vms,
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
    const dependsOn = buildDependencyColumns('dependsOn', vms.id, () => [])
    const usedBy = buildDependencyColumns('usedBy', identity.id, () => [])

    // Act / Assert
    expect(column(dependsOn, 'farEnd').header).toBe('Depends On')
    expect(column(dependsOn, 'farEnd').accessorFn(link())).toBe('Argo Identity')

    expect(column(usedBy, 'farEnd').header).toBe('Used By')
    expect(column(usedBy, 'farEnd').accessorFn(link())).toBe('Trio VMS')
  })

  it('leaves the near end empty on a link of the page product itself', () => {
    // Arrange
    const columns = buildDependencyColumns('dependsOn', vms.id, () => [])

    // Act / Assert
    expect(column(columns, 'nearEnd').accessorFn(link())).toBe('')
  })

  it('names the descendant a rolled-up link starts from', () => {
    // Arrange — Trio's page, showing a link recorded on Trio VMS
    const columns = buildDependencyColumns('dependsOn', trio.id, () => [])

    // Act / Assert
    expect(column(columns, 'nearEnd').header).toBe('From')
    expect(column(columns, 'nearEnd').accessorFn(link())).toBe('Trio VMS')
  })

  it('names the descendant a rolled-up link lands on', () => {
    // Arrange — a platform's page, showing a link to one of its services
    const columns = buildDependencyColumns('usedBy', 'argo', () => [])

    // Act / Assert
    expect(column(columns, 'nearEnd').header).toBe('Via')
    expect(column(columns, 'nearEnd').accessorFn(link())).toBe('Argo Identity')
  })
})

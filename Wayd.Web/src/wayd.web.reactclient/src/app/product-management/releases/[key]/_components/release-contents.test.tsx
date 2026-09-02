import { ReleaseDto } from '@/src/services/wayd-api'
import { buildContentsColumns, toEntries } from './release-contents'
import type { ContentsEntry } from './release-contents'

const release = (overrides: Partial<ReleaseDto> = {}): ReleaseDto =>
  ({
    id: 'r1',
    key: 29,
    version: '2026.09',
    status: { id: 's', name: 'Planned', category: 1, alias: 0 },
    versions: [],
    packages: [],
    ...overrides,
  }) as ReleaseDto

const withContents = () =>
  release({
    packages: [
      {
        package: { id: 'pk1', key: 12, name: 'WAYD-2026.09.1' },
        releasedDate: '2026-09-01' as unknown as Date,
      },
    ],
    versions: [
      {
        version: { id: 'v1', key: 29, name: '4.10.0' },
        product: { id: 'p1', key: 2, name: 'Wayd API' },
      },
    ],
  } as Partial<ReleaseDto>)

/** The value a column reports for a row — what sorting and the CSV export read. */
const valueOf = (columnId: string, entry: ContentsEntry) => {
  const column = buildContentsColumns().find((c) => c.id === columnId)
  if (!column) throw new Error(`no column ${columnId}`)

  const withAccessorFn = column as { accessorFn?: (row: ContentsEntry) => unknown }
  if (withAccessorFn.accessorFn) return withAccessorFn.accessorFn(entry)

  const withKey = column as { accessorKey?: keyof ContentsEntry }
  return withKey.accessorKey ? entry[withKey.accessorKey] : undefined
}

describe('toEntries', () => {
  it('flattens both routes into one list, packages first', () => {
    // Arrange / Act
    const entries = toEntries(withContents())

    // Assert
    // Packages lead because that is the usual route and carries most of what shipped.
    expect(entries.map((e) => e.route)).toEqual(['Package', 'Direct'])
  })

  it('carries the product name as text, not only as a rendered cell', () => {
    // Arrange / Act
    const entries = toEntries(withContents())

    // Assert
    // The rendered cell is a link, which a CSV export and a sort comparator cannot read. The text is
    // what the column's accessor returns, so it has to exist independently of the node.
    expect(entries[1].product).toBe('Wayd API')
  })

  it('leaves the product empty for a package', () => {
    // Arrange / Act
    const entries = toEntries(withContents())

    // Assert
    // A package is the shipment rather than the thing shipped, so it has no product of its own.
    expect(entries[0].product).toBe('')
  })

  it('leaves the product empty for a version that carries none', () => {
    // Arrange
    const sut = release({
      versions: [{ version: { id: 'v1', key: 29, name: '4.10.0' } }],
    } as Partial<ReleaseDto>)

    // Act
    const entries = toEntries(sut)

    // Assert
    expect(entries[0].product).toBe('')
  })
})

describe('buildContentsColumns', () => {
  it('gives every column its own id', () => {
    // Two columns sharing an id collide in column state — sizing, visibility and the column menu all
    // address a column by it, so the second silently takes the first's place.
    // Arrange / Act
    const ids = buildContentsColumns().map((c) => c.id)

    // Assert
    expect(new Set(ids).size).toBe(ids.length)
  })

  it('reports the product name as the Product column value', () => {
    // Arrange
    // The bug this guards: the column rendered the product correctly while its accessor returned the
    // route, so the CSV export wrote "Direct" under Product and sorting ordered by route while the
    // visible names looked unsorted. A cell renders; an accessor answers.
    const [, direct] = toEntries(withContents())

    // Act
    const value = valueOf('product', direct)

    // Assert
    expect(value).toBe('Wayd API')
    expect(value).not.toBe(direct.route)
  })

  it('reports the item name as the Item column value', () => {
    // Arrange
    const [pkg] = toEntries(withContents())

    // Act / Assert
    expect(valueOf('item', pkg)).toBe('WAYD-2026.09.1')
  })

  it('reports the route as the Route column value', () => {
    // Arrange
    const [pkg, direct] = toEntries(withContents())

    // Act / Assert
    expect(valueOf('route', pkg)).toBe('Package')
    expect(valueOf('route', direct)).toBe('Direct')
  })
})

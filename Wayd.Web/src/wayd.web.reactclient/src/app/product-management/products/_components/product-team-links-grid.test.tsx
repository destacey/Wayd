import { ProductTeamLinkDto, TeamType } from '@/src/services/wayd-api'
import { buildProductTeamLinkColumns } from './product-team-links-grid'

// The actions hook pulls in the dialogs; the columns under test only take its item builder.
jest.mock('./use-product-team-link-actions', () => ({
  __esModule: true,
  default: () => ({ getActionItems: () => [], dialogs: null }),
}))

const storefront = { id: 'storefront', key: 1, name: 'Storefront' }
const web = { id: 'web', key: 2, name: 'Storefront Web' }

const link = (
  overrides: Partial<ProductTeamLinkDto> = {},
): ProductTeamLinkDto =>
  ({
    product: web,
    productPath: [storefront],
    team: {
      id: 'payments',
      key: 7,
      name: 'Payments',
      code: 'PAY',
      type: TeamType.Team,
      isActive: true,
    },
    ...overrides,
  }) as ProductTeamLinkDto

const column = (
  columns: ReturnType<typeof buildProductTeamLinkColumns>,
  id: string,
) =>
  columns.find((c) => c.id === id) as unknown as {
    accessorFn: (row: ProductTeamLinkDto) => string
  }

describe('buildProductTeamLinkColumns', () => {
  it('leads with the product', () => {
    // Arrange
    const columns = buildProductTeamLinkColumns(() => [])

    // Act / Assert
    expect(columns.map((c) => c.id)).toEqual(['actions', 'product', 'path'])
    expect(column(columns, 'product').accessorFn(link())).toBe('Storefront Web')
  })

  it('shows where each product sits in the catalog', () => {
    // Arrange
    const columns = buildProductTeamLinkColumns(() => [])

    // Act / Assert
    expect(
      column(columns, 'path').accessorFn(
        link({
          productPath: [{ id: 'a', key: 9, name: 'Commerce' }, storefront],
        }),
      ),
    ).toBe('Commerce › Storefront')
  })
})

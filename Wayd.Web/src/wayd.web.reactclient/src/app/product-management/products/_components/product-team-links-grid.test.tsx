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

const ids = (columns: ReturnType<typeof buildProductTeamLinkColumns>) =>
  columns.map((c) => c.id)

const column = (
  columns: ReturnType<typeof buildProductTeamLinkColumns>,
  id: string,
) =>
  columns.find((c) => c.id === id) as unknown as {
    accessorFn: (row: ProductTeamLinkDto) => string
  }

describe('buildProductTeamLinkColumns', () => {
  it('leads with the team on a product and with the product on a team', () => {
    // Arrange
    const onProduct = buildProductTeamLinkColumns(
      { kind: 'product', productId: web.id },
      () => [],
    )
    const onTeam = buildProductTeamLinkColumns({ kind: 'team' }, () => [])

    // Act / Assert
    expect(ids(onProduct)).toContain('team')
    expect(ids(onProduct)).not.toContain('product')
    expect(column(onProduct, 'team').accessorFn(link())).toBe('Payments')

    expect(ids(onTeam)).toContain('product')
    expect(ids(onTeam)).not.toContain('team')
    expect(column(onTeam, 'product').accessorFn(link())).toBe('Storefront Web')
  })

  it('leaves On empty for the page product and names the descendant a rolled-up link is on', () => {
    // Arrange
    const onWeb = buildProductTeamLinkColumns(
      { kind: 'product', productId: web.id },
      () => [],
    )
    const onStorefront = buildProductTeamLinkColumns(
      { kind: 'product', productId: storefront.id },
      () => [],
    )

    // Act / Assert
    expect(column(onWeb, 'on').accessorFn(link())).toBe('')
    expect(column(onStorefront, 'on').accessorFn(link())).toBe('Storefront Web')
  })

  it("shows where each product sits on a team's list", () => {
    // Arrange
    const columns = buildProductTeamLinkColumns({ kind: 'team' }, () => [])

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

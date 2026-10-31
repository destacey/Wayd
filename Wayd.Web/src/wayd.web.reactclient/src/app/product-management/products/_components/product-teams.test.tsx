import { render, screen } from '@testing-library/react'
import {
  ProductTeamLinkDto,
  ProductTeamNavigationDto,
  TeamType,
} from '@/src/services/wayd-api'
import ProductTeams, { productTeamCount, teamsBeneath } from './product-teams'

const team = (
  id: string,
  key: number,
  name: string,
  overrides: Partial<ProductTeamNavigationDto> = {},
): ProductTeamNavigationDto => ({
  id,
  key,
  name,
  code: name.slice(0, 3).toUpperCase(),
  type: TeamType.Team,
  isActive: true,
  ...overrides,
})

const payments = team('payments', 7, 'Payments')
const atlas = team('atlas', 8, 'Atlas', { type: TeamType.TeamOfTeams })

const web = { id: 'web', key: 2, name: 'Storefront Web' }
const checkout = { id: 'checkout', key: 3, name: 'Checkout' }
const api = { id: 'api', key: 4, name: 'Storefront API' }

const link = (
  product: typeof web,
  linked: ProductTeamNavigationDto,
): ProductTeamLinkDto =>
  ({ product, productPath: [], team: linked }) as unknown as ProductTeamLinkDto

// Storefront Web is the page; Checkout and Storefront API sit beneath it.
const LINKS = [
  link(web, payments),
  link(checkout, payments),
  link(api, payments),
  link(checkout, atlas),
]

describe('teamsBeneath', () => {
  it("lists each team once, with the products beneath it is on, leaving out the page's own links", () => {
    // Act
    const result = teamsBeneath(web.id, LINKS)

    // Assert
    expect(
      result.map((r) => [r.team.name, r.products.map((p) => p.name)]),
    ).toEqual([
      ['Atlas', ['Checkout']],
      ['Payments', ['Checkout', 'Storefront API']],
    ])
  })
})

describe('productTeamCount', () => {
  it('counts a team on the product and beneath it once', () => {
    // Act / Assert
    expect(productTeamCount([payments], LINKS)).toBe(2)
  })
})

describe('ProductTeams', () => {
  it("shows the product's own teams as cards, by name, saying which are teams of teams", () => {
    // Arrange / Act
    render(
      <ProductTeams
        productId={web.id}
        teams={[payments, atlas]}
        links={[]}
        isLoading={false}
      />,
    )

    // Assert
    const names = screen.getAllByRole('link').map((a) => a.textContent)
    expect(names).toEqual(['Atlas', 'Payments'])
    expect(screen.getByText('Team of Teams')).toBeInTheDocument()
    expect(
      screen.queryByText('Teams on products beneath'),
    ).not.toBeInTheDocument()
  })

  it('lists the teams on products beneath, naming the products each is on', () => {
    // Arrange / Act
    render(
      <ProductTeams
        productId={web.id}
        teams={[]}
        links={LINKS}
        isLoading={false}
      />,
    )

    // Assert
    expect(
      screen.getByText('No team is linked to this product.'),
    ).toBeInTheDocument()
    expect(screen.getByText('Teams on products beneath')).toBeInTheDocument()
    expect(
      screen.getByRole('link', { name: 'Storefront API' }),
    ).toHaveAttribute('href', '/product-management/products/4')
  })

  it('marks an inactive team', () => {
    // Arrange / Act
    render(
      <ProductTeams
        productId={web.id}
        teams={[team('legacy', 9, 'Legacy Ops', { isActive: false })]}
        links={[]}
        isLoading={false}
      />,
    )

    // Assert
    expect(screen.getByText('Inactive')).toBeInTheDocument()
  })
})

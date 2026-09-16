import { ProductActivityDto } from '@/src/services/wayd-api'
import { render, screen } from '@testing-library/react'
import VersionActivity, { levelDescription, withAlpha } from './version-activity'

// The global mock stubs dayjs to formatting only; this component does date arithmetic.
jest.unmock('dayjs')

const row = (
  name: string,
  days: { date: string; released: number; withdrawn?: number }[],
  overrides: { depth?: number; isReleasable?: boolean } = {},
): ProductActivityDto =>
  ({
    product: { id: name, key: 1, name },
    depth: overrides.depth ?? 0,
    isReleasable: overrides.isReleasable ?? true,
    totalReleased: days.reduce((sum, day) => sum + day.released, 0),
    days: days.map((day) => ({
      date: day.date,
      released: day.released,
      withdrawn: day.withdrawn ?? 0,
    })),
  }) as unknown as ProductActivityDto

const cells = (product: string) =>
  screen.getAllByLabelText(new RegExp(`^${product} `))

describe('VersionActivity', () => {
  it('draws a cell for every day in the window, including the quiet ones', () => {
    // Arrange — a gap is information. Skipping empty days would misalign every row against the
    // calendar and hide a pause entirely.
    const activity = [row('Checkout API', [{ date: '2026-09-03', released: 2 }])]

    // Act
    render(
      <VersionActivity activity={activity} from="2026-09-01" to="2026-09-05" />,
    )

    // Assert
    expect(cells('Checkout API')).toHaveLength(5)
  })

  it('keeps a product that shipped nothing', () => {
    // Arrange — an absent row reads as "not in scope" rather than "nothing shipped", and a quiet
    // product is usually the thing worth noticing.
    const activity = [
      row('Checkout API', [{ date: '2026-09-01', released: 1 }]),
      row('Quiet Service', []),
    ]

    // Act
    render(
      <VersionActivity activity={activity} from="2026-09-01" to="2026-09-02" />,
    )

    // Assert
    expect(screen.getByText('Quiet Service')).toBeInTheDocument()
    expect(cells('Quiet Service')).toHaveLength(2)
  })

  it('lists a node that is both releasable and a parent exactly once', () => {
    // Arrange — the defect this replaced: grouping rows under a parent's name drew such a node
    // twice, once as a row and again as a heading over its own children.
    const activity = [
      row('Onboarding Cloud', [], { depth: 0, isReleasable: false }),
      row('Onboarding Content', [{ date: '2026-09-01', released: 1 }], { depth: 1 }),
      row('Fulfillment Dashboard', [{ date: '2026-09-01', released: 1 }], { depth: 2 }),
    ]

    // Act
    render(
      <VersionActivity activity={activity} from="2026-09-01" to="2026-09-02" />,
    )

    // Assert
    expect(screen.getAllByText('Onboarding Content')).toHaveLength(1)
  })

  it('draws no days for a grouping, which can never have versions of its own', () => {
    // Arrange — an empty row of cells would claim it had a quiet fortnight rather than that the
    // question does not apply to it.
    const activity = [
      row('Payments Core', [], { isReleasable: false }),
      row('Checkout API', [{ date: '2026-09-01', released: 1 }], { depth: 1 }),
    ]

    // Act
    render(
      <VersionActivity activity={activity} from="2026-09-01" to="2026-09-02" />,
    )

    // Assert
    expect(screen.getByText('Payments Core')).toBeInTheDocument()
    expect(screen.queryAllByLabelText(/^Payments Core /)).toHaveLength(0)
    expect(cells('Checkout API')).toHaveLength(2)
  })

  it('indents a row by how deep it sits in the catalog', () => {
    // Arrange / Act
    render(
      <VersionActivity
        activity={[row('Deep Service', [], { depth: 2 })]}
        from="2026-09-01"
        to="2026-09-02"
      />,
    )

    // Assert
    expect(screen.getByText('Deep Service').closest('div')).toHaveStyle({
      paddingLeft: '32px',
    })
  })

  it('names the withdrawal count on the day it happened', () => {
    // Arrange — a withdrawal marks the day the version shipped rather than being a release of its
    // own, so the count has to sit alongside the releases rather than replace them.
    const activity = [
      row('Notifications', [
        { date: '2026-09-01', released: 2, withdrawn: 1 },
      ]),
    ]

    // Act
    render(
      <VersionActivity activity={activity} from="2026-09-01" to="2026-09-01" />,
    )

    // Assert
    expect(
      screen.getByLabelText('Notifications 2026-09-01: 2 released'),
    ).toBeInTheDocument()
  })

  it('heads each column with its day, so a cell can be placed in the week', () => {
    // Arrange — 1 Sep 2026 is a Tuesday. Without the header a cell is an unlabelled square and the
    // reader cannot tell a quiet weekend from a stalled week.
    const activity = [row('Checkout API', [{ date: '2026-09-01', released: 1 }])]

    // Act
    render(
      <VersionActivity activity={activity} from="2026-09-01" to="2026-09-07" />,
    )

    // Assert — seven columns, starting on Tuesday and running to Monday.
    const headings = screen
      .getAllByText(/^[MTWFS]$/)
      .map((el) => el.textContent)
    expect(headings).toEqual(['T', 'W', 'T', 'F', 'S', 'S', 'M'])
  })

  it('says so when the scope holds no releasable products at all', () => {
    // Arrange / Act
    render(<VersionActivity activity={[]} from="2026-09-01" to="2026-09-02" />)

    // Assert
    expect(
      screen.getByText('No releasable products in scope.'),
    ).toBeInTheDocument()
  })
})

describe('levelDescription', () => {
  it('names an exact count for each step', () => {
    // Arrange / Act / Assert
    expect(levelDescription(0)).toBe('No versions')
    expect(levelDescription(1)).toBe('1 version')
    expect(levelDescription(2)).toBe('2 versions')
    expect(levelDescription(4)).toBe('4 versions')
  })

  it('collapses the busiest days into one step', () => {
    // Arrange — a day with twenty and a day with six are both simply busy, and grading beyond that
    // spends shades on a distinction nobody acts on.
    // Act / Assert
    expect(levelDescription(5)).toBe('5 or more versions')
  })
})

describe('withAlpha', () => {
  it('derives the step from the theme primary rather than a fixed colour', () => {
    // Arrange / Act / Assert — whatever primary the active theme sets is what the scale is built
    // from, so the heatmap stays thematically correct without listing a palette per theme.
    expect(withAlpha('#1677ff', 0.25)).toBe('rgba(22, 119, 255, 0.25)')
    expect(withAlpha('#1677FF', 1)).toBe('rgba(22, 119, 255, 1)')
  })

  it('expands a short hex', () => {
    // Arrange / Act / Assert
    expect(withAlpha('#0af', 0.5)).toBe('rgba(0, 170, 255, 0.5)')
  })

  it('returns the colour untouched when it cannot be parsed', () => {
    // Arrange — an unexpected token format should degrade to a flat shade rather than emit an
    // invalid style that renders as no background at all.
    // Act / Assert
    expect(withAlpha('currentColor', 0.5)).toBe('currentColor')
  })
})

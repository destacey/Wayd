// jest.setup stubs dayjs down to formatting, which shifts calendar dates; the need-by tag needs the real one.
jest.unmock('dayjs')

import { render, screen } from '@testing-library/react'
import {
  IntakeRequestSummaryDto,
  NeedByBasis,
  NeedByBasisFamily,
  StatusCategory,
  StatusNavigationDto,
} from '@/src/services/wayd-api'
import RequestBoard, { boardColumns } from './request-board'

const status = (
  id: string,
  name: string,
  category: StatusCategory,
): StatusNavigationDto => ({
  id,
  name,
  category,
  categoryLabel: category,
  alias: 0,
})

const STATUSES = [
  status('s-submitted', 'Submitted', StatusCategory.Proposed),
  status('s-review', 'In Review', StatusCategory.Proposed),
  status('s-accepted', 'Accepted', StatusCategory.Active),
  status('s-declined', 'Declined', StatusCategory.Removed),
]

const request = (
  key: number,
  statusId: string,
  overrides: Partial<IntakeRequestSummaryDto> = {},
): IntakeRequestSummaryDto =>
  ({
    id: `r-${key}`,
    key,
    title: `Request ${key}`,
    status: STATUSES.find((s) => s.id === statusId),
    desk: { id: 'd-1', key: 1, name: 'Product' },
    requester: { id: 'e-1', key: 1, name: 'Avery Example' },
    channel: { id: 'c-1', name: 'Support' },
    submittedOn: new Date(),
    statusChangedOn: new Date(),
    ...overrides,
  }) as IntakeRequestSummaryDto

describe('boardColumns', () => {
  it('gives every open stage a column in the workflow’s order, empty ones too', () => {
    // Arrange
    const requests = [request(1, 's-accepted'), request(2, 's-submitted')]

    // Act
    const columns = boardColumns(STATUSES, requests, 'open')

    // Assert
    expect(columns.map((c) => c.status.name)).toEqual([
      'Submitted',
      'In Review',
      'Accepted',
    ])
    expect(columns.map((c) => c.requests.length)).toEqual([1, 0, 1])
  })

  it('adds the closed stages when everything is in scope', () => {
    // Act
    const columns = boardColumns(STATUSES, [], 'all')

    // Assert
    expect(columns.map((c) => c.status.name)).toContain('Declined')
  })
})

describe('RequestBoard', () => {
  it('shows each request as a card linking to it, in its stage', () => {
    // Arrange
    const columns = boardColumns(
      STATUSES,
      [
        request(42, 's-review', {
          needBy: {
            date: '2026-12-31',
            basis: NeedByBasis.Regulatory,
            family: NeedByBasisFamily.FixedForUs,
            reference: 'PCI DSS 4.0',
          },
        }),
      ],
      'open',
    )

    // Act
    render(<RequestBoard columns={columns} />)

    // Assert
    const stage = screen.getByRole('region', { name: 'In Review, 1' })
    expect(stage).toHaveTextContent('Request 42')
    expect(stage).toHaveTextContent('By Dec 31, 2026 · a regulation sets it')
    expect(screen.getByRole('link', { name: /Request 42/ })).toHaveAttribute(
      'href',
      '/demand-management/requests/42',
    )
  })

  it('shows one stage at a time on a phone, starting with the first that has requests', () => {
    // Arrange — jsdom matches no breakpoint, so the board renders as it does below md
    const columns = boardColumns(STATUSES, [request(7, 's-accepted')], 'open')

    // Act
    render(<RequestBoard columns={columns} />)

    // Assert
    expect(screen.getAllByRole('region')).toHaveLength(1)
    expect(
      screen.getByRole('region', { name: 'Accepted, 1' }),
    ).toBeInTheDocument()
    expect(screen.getByRole('combobox', { name: 'Stage' })).toBeInTheDocument()
  })
})

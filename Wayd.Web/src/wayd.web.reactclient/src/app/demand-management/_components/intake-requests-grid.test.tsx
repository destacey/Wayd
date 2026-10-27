// jest.setup stubs dayjs down to formatting; the grid's date filter needs the real module.
jest.unmock('dayjs')

import { render, screen } from '@testing-library/react'
import {
  IntakeRequestSummaryDto,
  NeedByBasis,
  NeedByBasisDto,
  NeedByBasisFamily,
  StatusCategory,
} from '@/src/services/wayd-api'
import IntakeRequestsGrid from './intake-requests-grid'

const DAY_MS = 24 * 60 * 60 * 1000
const daysAgo = (days: number) => new Date(Date.now() - days * DAY_MS - 1000)

const request = (
  key: number,
  title: string,
  needBy?: IntakeRequestSummaryDto['needBy'],
  ages: { submitted?: number; inStatus?: number } = {},
): IntakeRequestSummaryDto =>
  ({
    id: `r-${key}`,
    key,
    title,
    desk: { id: 'd-1', key: 1, name: 'IT' },
    requester: { id: 'e-1', key: 1, name: 'Avery Example' },
    channel: { id: 'c-1', name: 'Security' },
    status: {
      id: 's-1',
      name: 'Waiting on Requester',
      category: StatusCategory.Proposed,
      alias: 2,
    },
    needBy,
    submittedOn: daysAgo(ages.submitted ?? 0),
    statusChangedOn: daysAgo(ages.inStatus ?? 0),
  }) as IntakeRequestSummaryDto

const BASES: NeedByBasisDto[] = [
  {
    id: 2,
    code: NeedByBasis.Contractual,
    name: 'Contract term',
    order: 2,
    family: NeedByBasisFamily.FixedForUs,
    recordsCommitment: false,
  },
]

const renderGrid = (requests: IntakeRequestSummaryDto[]) =>
  render(
    <IntakeRequestsGrid
      requests={requests}
      isLoading={false}
      refetch={() => {}}
      needByBases={BASES}
      persistStateKey="test-requests"
      csvFileName="requests"
    />,
  )

describe('IntakeRequestsGrid', () => {
  it('shows the status of each request as its desk workflow names it', () => {
    // Arrange / Act
    renderGrid([request(42, 'Rotate the VPN certificates')])

    // Assert
    expect(screen.getByText('Waiting on Requester')).toBeInTheDocument()
  })
  it('links the number and the title to the request page', () => {
    // Arrange / Act
    renderGrid([request(42, 'Rotate the VPN certificates')])

    // Assert
    expect(screen.getByRole('link', { name: '42' })).toHaveAttribute(
      'href',
      '/demand-management/requests/42',
    )
    expect(
      screen.getByRole('link', { name: 'Rotate the VPN certificates' }),
    ).toHaveAttribute('href', '/demand-management/requests/42')
  })

  it('shows the need-by date with what makes it that date', () => {
    // Arrange / Act
    renderGrid([
      request(7, 'Renew the support contract', {
        date: '2026-12-31',
        basis: NeedByBasis.Contractual,
        family: NeedByBasisFamily.FixedForUs,
        reference: 'MSA clause 12',
      }),
    ])

    // Assert
    expect(
      screen.getByText('By Dec 31, 2026 · a contract names it'),
    ).toBeInTheDocument()
  })

  it('shows how long a request has waited, and how long in its status', () => {
    // Arrange / Act
    renderGrid([
      request(9, 'Laptop refresh', undefined, { submitted: 12, inStatus: 3 }),
    ])

    // Assert
    expect(screen.getByText('12d')).toBeInTheDocument()
    expect(screen.getByText('3d')).toBeInTheDocument()
  })
})

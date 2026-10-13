import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import TeamCapacityRollup from './team-capacity-rollup'

jest.unmock('dayjs')

const range = { from: '2027-02-01', to: '2027-02-28' }

const person = (id: string, key: number, name: string, allocated: number) => ({
  employee: { id, key, name },
  allocatedPersonDays: allocated,
  deliveryPersonDays: allocated,
  allocations: [],
})

const nodes = [
  {
    team: { id: 't-1', key: 1, name: 'Cards', code: 'CARDS', type: 'Team' },
    periods: [{ start: '2027-02-01', end: '2027-02-28' }],
    workingDays: 20,
    allocatedPersonDays: 0,
    deliveryPersonDays: 0,
    directAllocatedPersonDays: 0,
    directDeliveryPersonDays: 0,
    people: [],
    children: [],
  },
  {
    team: {
      id: 't-2',
      key: 2,
      name: 'Lending',
      code: 'LEND',
      type: 'Team of Teams',
    },
    periods: [{ start: '2027-02-08', end: '2027-02-28' }],
    workingDays: 15,
    allocatedPersonDays: 13.5,
    deliveryPersonDays: 12,
    directAllocatedPersonDays: 1.5,
    directDeliveryPersonDays: 0,
    people: [person('e-1', 11, 'Grace Hopper', 1.5)],
    children: [
      {
        team: { id: 't-3', key: 3, name: 'Loans', code: 'LOANS', type: 'Team' },
        periods: [{ start: '2027-02-08', end: '2027-02-28' }],
        workingDays: 12,
        allocatedPersonDays: 12,
        deliveryPersonDays: 12,
        directAllocatedPersonDays: 12,
        directDeliveryPersonDays: 12,
        people: [person('e-2', 12, 'Ada Lovelace', 12)],
        children: [],
      },
    ],
  },
] as never

const renderRollup = () =>
  render(
    <TeamCapacityRollup
      nodes={nodes}
      range={range}
      isLoading={false}
      onRefresh={jest.fn()}
    />,
  )

describe('TeamCapacityRollup', () => {
  it('lists the teams directly beneath, each linked to its own page, with what is beneath them closed', () => {
    renderRollup()

    expect(screen.getByRole('link', { name: 'Cards' })).toHaveAttribute(
      'href',
      '/organizations/teams/1',
    )
    expect(screen.getByRole('link', { name: 'Lending' })).toHaveAttribute(
      'href',
      '/organizations/team-of-teams/2',
    )
    expect(screen.getByText('13.5')).toBeInTheDocument()
    expect(screen.queryByText('Grace Hopper')).not.toBeInTheDocument()
    expect(screen.queryByText('Loans')).not.toBeInTheDocument()
  })

  it('keeps a team with nothing allocated on the list at zero', () => {
    renderRollup()

    const row = screen.getByRole('link', { name: 'Cards' }).closest('tr')
    expect(row).toHaveTextContent('Whole range')
    expect(row).toHaveTextContent('20')
    expect(
      screen.queryByRole('button', { name: 'Expand Cards' }),
    ).not.toBeInTheDocument()
  })

  it('drills down into a team of teams to its own people and then its teams', async () => {
    renderRollup()

    await userEvent.click(
      screen.getByRole('button', { name: 'Expand Lending' }),
    )

    expect(screen.getByRole('link', { name: 'Grace Hopper' })).toHaveAttribute(
      'href',
      '/organizations/employees/11',
    )
    expect(screen.getByRole('link', { name: 'Loans' })).toBeInTheDocument()
    expect(screen.queryByText('Ada Lovelace')).not.toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Expand Loans' }))

    expect(
      screen.getByRole('link', { name: 'Ada Lovelace' }),
    ).toBeInTheDocument()
  })

  it('offers nothing that changes an allocation', async () => {
    renderRollup()
    await userEvent.click(
      screen.getByRole('button', { name: 'Expand Lending' }),
    )

    for (const name of ['Change', 'Rebalance', 'Allocate Person', 'Initialize'])
      expect(screen.queryByRole('button', { name })).not.toBeInTheDocument()
  })
})

import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import InitializeAllocationsForm from './initialize-allocations-form'

jest.unmock('dayjs')

// Mock window.getComputedStyle for Ant Design Modal
Object.defineProperty(window, 'getComputedStyle', {
  value: () => ({
    getPropertyValue: () => '',
  }),
})

const mockMessageSuccess = jest.fn()
const mockMessageError = jest.fn()
jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({
    success: mockMessageSuccess,
    error: mockMessageError,
  }),
}))

const mockChangeAllocations = jest.fn()
jest.mock('@/src/store/features/organization/employee-allocations-api', () => ({
  useChangeEmployeeAllocationsMutation: () => [mockChangeAllocations],
}))

let mockRows: unknown[] = []
jest.mock('@/src/store/features/organization/team-capacity-api', () => ({
  useGetTeamAllocationProposalQuery: () => ({
    data: { from: '2027-02-01', rows: mockRows },
    isFetching: false,
  }),
}))

const row = (
  employee: { id: string; name: string },
  role: { id: string; name: string },
  remainingPercent: number,
  percent: number,
) => ({
  employee: { ...employee, key: 1 },
  role: { ...role, key: 1 },
  countsTowardDeliveryCapacity: true,
  remainingPercent,
  percent,
})

const ada = { id: 'e-1', name: 'Ada Lovelace' }
const grace = { id: 'e-2', name: 'Grace Hopper' }
const alan = { id: 'e-3', name: 'Alan Turing' }
const engineer = { id: 'r-1', name: 'Engineer' }
const lead = { id: 'r-2', name: 'Lead' }

const renderForm = (onFormComplete = jest.fn(), onFormCancel = jest.fn()) =>
  render(
    <InitializeAllocationsForm
      teamId="team-1"
      teamType="Team"
      from="2027-02-01"
      onFormComplete={onFormComplete}
      onFormCancel={onFormCancel}
    />,
  )

describe('InitializeAllocationsForm', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockChangeAllocations.mockResolvedValue({ data: undefined })
    mockRows = [
      row(ada, engineer, 55, 28),
      row(ada, lead, 55, 27),
      row(grace, engineer, 100, 100),
      row(alan, engineer, 0, 0),
    ]
  })

  it('saves nothing when the proposal is shown and then cancelled', async () => {
    const onFormCancel = jest.fn()
    renderForm(jest.fn(), onFormCancel)

    expect(await screen.findByText('Grace Hopper')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Cancel' }))

    expect(mockChangeAllocations).not.toHaveBeenCalled()
    expect(onFormCancel).toHaveBeenCalledTimes(1)
  })

  it('flags a member with nothing left instead of leaving them out', async () => {
    renderForm()

    expect(await screen.findByText('Alan Turing')).toBeInTheDocument()
    expect(screen.getByText('Nothing left')).toBeInTheDocument()
    expect(
      screen.getByRole('spinbutton', {
        name: 'Percent for Alan Turing as Engineer',
      }),
    ).toBeDisabled()
  })

  it('saves one change per person, open-ended from the first day, once confirmed', async () => {
    const onFormComplete = jest.fn()
    renderForm(onFormComplete)

    await userEvent.click(await screen.findByRole('button', { name: 'Save' }))

    await waitFor(() => expect(onFormComplete).toHaveBeenCalledTimes(1))
    expect(mockChangeAllocations.mock.calls.map(([arg]) => arg)).toEqual([
      {
        employeeId: 'e-1',
        request: {
          changes: [
            {
              teamId: 'team-1',
              roleId: 'r-1',
              start: '2027-02-01',
              end: undefined,
              percent: 28,
            },
            {
              teamId: 'team-1',
              roleId: 'r-2',
              start: '2027-02-01',
              end: undefined,
              percent: 27,
            },
          ],
        },
      },
      {
        employeeId: 'e-2',
        request: {
          changes: [
            {
              teamId: 'team-1',
              roleId: 'r-1',
              start: '2027-02-01',
              end: undefined,
              percent: 100,
            },
          ],
        },
      },
    ])
    expect(mockMessageSuccess).toHaveBeenCalledWith('Allocations saved.')
  })

  it('saves the percent the planner entered', async () => {
    renderForm()

    const input = await screen.findByRole('spinbutton', {
      name: 'Percent for Grace Hopper as Engineer',
    })
    await userEvent.clear(input)
    await userEvent.type(input, '40')
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(mockChangeAllocations).toHaveBeenCalledTimes(2))
    expect(
      mockChangeAllocations.mock.calls[1][0].request.changes[0].percent,
    ).toBe(40)
  })

  it('blocks saving while a person is proposed more than they have left', async () => {
    renderForm()

    const input = await screen.findByRole('spinbutton', {
      name: 'Percent for Ada Lovelace as Lead',
    })
    await userEvent.clear(input)
    await userEvent.type(input, '30')

    expect(
      await screen.findByText(
        "A person's rows add up to more than they have left.",
      ),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled()
    expect(mockChangeAllocations).not.toHaveBeenCalled()
  })

  it('stays open on the people it failed for, with the reason, and retries only them', async () => {
    mockChangeAllocations
      .mockResolvedValueOnce({
        error: {
          status: 400,
          detail: 'The employee would be 120% allocated.',
        },
      })
      .mockResolvedValue({ data: undefined })
    const onFormComplete = jest.fn()
    renderForm(onFormComplete)

    // The form's state changes after the awaited saves, which only render
    // when the click runs inside act.
    const save = await screen.findByRole('button', { name: 'Save' })
    await act(async () => {
      fireEvent.click(save)
    })

    expect(
      await screen.findByText(
        'Ada Lovelace: The employee would be 120% allocated.',
      ),
    ).toBeInTheDocument()
    expect(onFormComplete).not.toHaveBeenCalled()
    expect(mockChangeAllocations).toHaveBeenCalledTimes(2)
    expect(screen.queryByText('Grace Hopper')).not.toBeInTheDocument()

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Save' }))
    })

    await waitFor(() => expect(onFormComplete).toHaveBeenCalledTimes(1))
    expect(mockChangeAllocations).toHaveBeenCalledTimes(3)
    expect(mockChangeAllocations.mock.calls[2][0].employeeId).toBe('e-1')
  })

  it('ends a row the day before time the team already has, and says so', async () => {
    mockRows = [{ ...row(grace, engineer, 80, 80), end: '2027-02-28' }]
    renderForm()

    expect(await screen.findByText('Feb 28, 2027')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(mockChangeAllocations).toHaveBeenCalledTimes(1))
    expect(
      mockChangeAllocations.mock.calls[0][0].request.changes[0],
    ).toMatchObject({
      start: '2027-02-01',
      end: '2027-02-28',
      percent: 80,
    })
  })
})

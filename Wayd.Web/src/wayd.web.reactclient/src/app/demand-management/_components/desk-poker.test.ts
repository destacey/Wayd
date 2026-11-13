import {
  IntakeDeskDto,
  IntakeRequestSummaryDto,
  StatusCategory,
} from '@/src/services/wayd-api'
import { canRunDeskPoker, pokerSelection } from './desk-poker'

const desk = (
  id: string,
  memberIds: string[],
  estimates = true,
): IntakeDeskDto =>
  ({
    id,
    key: 1,
    name: `Desk ${id}`,
    estimationScale: estimates ? { id: 's-1', name: 'T-Shirt' } : undefined,
    members: memberIds.map((employeeId) => ({ employee: { id: employeeId } })),
  }) as unknown as IntakeDeskDto

const request = (
  id: string,
  deskId: string,
  category = StatusCategory.Proposed,
): IntakeRequestSummaryDto =>
  ({
    id,
    desk: { id: deskId },
    status: { category },
  }) as unknown as IntakeRequestSummaryDto

describe('pokerSelection', () => {
  const desks = [desk('d-1', ['e-1']), desk('d-2', ['e-1'])]

  it('returns the desk and the requests, in the order selected', () => {
    // Act
    const result = pokerSelection(
      [request('r-2', 'd-1'), request('r-1', 'd-1', StatusCategory.Active)],
      desks,
      'e-1',
    )

    // Assert
    expect(result).toEqual({ desk: desks[0], requestIds: ['r-2', 'r-1'] })
  })

  it.each([
    ['nothing is selected', [], 'Select the requests to estimate.'],
    [
      'the requests are on different desks',
      [request('r-1', 'd-1'), request('r-2', 'd-2')],
      'Select requests from one desk: a session belongs to a desk.',
    ],
    [
      'a request is closed',
      [request('r-1', 'd-1'), request('r-2', 'd-1', StatusCategory.Done)],
      'Only open requests can be estimated.',
    ],
  ])('refuses when %s', (_why, selected, refusal) => {
    // Act
    const result = pokerSelection(selected, desks, 'e-1')

    // Assert
    expect(result).toEqual({ refusal })
  })

  it('refuses a reader who does not work the desk', () => {
    // Act
    const result = pokerSelection([request('r-1', 'd-1')], desks, 'e-9')

    // Assert
    expect(result).toEqual({
      refusal: 'Only agents on the desk can start its poker sessions.',
    })
  })

  it('refuses a desk with no estimation scale, naming it', () => {
    // Act
    const result = pokerSelection(
      [request('r-1', 'd-3')],
      [desk('d-3', ['e-1'], false)],
      'e-1',
    )

    // Assert
    expect(result).toEqual({ refusal: 'Desk d-3 has no estimation scale.' })
  })
})

describe('canRunDeskPoker', () => {
  it.each([
    ['a member of an estimating desk', desk('d-1', ['e-1']), 'e-1', true],
    ['someone off the desk', desk('d-1', ['e-2']), 'e-1', false],
    ['a desk with no scale', desk('d-1', ['e-1'], false), 'e-1', false],
    ['an account with no employee', desk('d-1', ['e-1']), null, false],
  ])('is as expected for %s', (_who, target, employeeId, expected) => {
    // Act
    const result = canRunDeskPoker(target, employeeId)

    // Assert
    expect(result).toBe(expected)
  })
})

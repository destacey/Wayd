import { IntakeDeskRole, IntakeDeskVisibility } from '@/src/services/wayd-api'
import { toUpdateRequest, triagerIds } from './desk-update'

const desk = {
  id: 'd-1',
  key: 1,
  name: 'IT',
  description: 'Laptops, access and software',
  visibility: IntakeDeskVisibility.Open,
  isOpen: true,
  isRequesterSelectable: true,
  requesterMayAddParticipants: true,
  members: [
    {
      employee: { id: 'e-1', key: 1, name: 'Avery' },
      role: IntakeDeskRole.Admin,
      isTriager: true,
    },
    {
      employee: { id: 'e-2', key: 2, name: 'Blake' },
      role: IntakeDeskRole.Agent,
      isTriager: false,
    },
  ],
  revision: 4,
} as any

describe('toUpdateRequest', () => {
  it('leaves the triagers out, so a form that does not edit them never resets them', () => {
    // Act
    const request = toUpdateRequest(desk, {
      members: [{ employeeId: 'e-1', role: IntakeDeskRole.Admin }],
    })

    // Assert
    expect(JSON.parse(JSON.stringify(request))).not.toHaveProperty('triagerIds')
  })

  it('sends the triagers a form chose, even none', () => {
    // Act
    const request = toUpdateRequest(desk, { triagerIds: [] })

    // Assert
    expect(request.triagerIds).toEqual([])
  })
})

describe('triagerIds', () => {
  it('lists only the members who triage', () => {
    // Act
    const ids = triagerIds(desk)

    // Assert
    expect(ids).toEqual(['e-1'])
  })
})

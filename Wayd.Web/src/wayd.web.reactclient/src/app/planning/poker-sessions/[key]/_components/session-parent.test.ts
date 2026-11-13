import { IntakeDeskDto, PokerSessionHostKind } from '@/src/services/wayd-api'
import { sessionParent, worksDesk } from './session-parent'

const deskHost = { kind: PokerSessionHostKind.IntakeDesk, id: 'd-1' }
const productDelivery = { key: 4, name: 'Product delivery' }

describe('sessionParent', () => {
  it.each([
    ['no host', undefined],
    ['a null host, as the API sends a standalone session', null],
  ])(
    'leads a session with %s back to the Planning Poker page',
    (_what, host) => {
      // Act
      const parent = sessionParent(host, undefined, false)

      // Assert
      expect(parent).toEqual({
        label: 'Planning Poker',
        href: '/planning/poker-sessions',
      })
    },
  )

  it('links someone who works the desk back to its poker sessions, by name and key', () => {
    // Act
    const parent = sessionParent(deskHost, productDelivery, true)

    // Assert
    expect(parent).toEqual([
      { label: 'Desks', href: '/demand-management/desks' },
      {
        label: 'Product delivery',
        href: '/demand-management/desks/4?section=poker-sessions',
      },
    ])
  })

  it('names the desk without linking it for an invited voter', () => {
    // Act
    const parent = sessionParent(deskHost, productDelivery, false)

    // Assert
    expect(parent).toEqual([
      { label: 'Desks', href: undefined },
      { label: 'Product delivery', href: undefined },
    ])
  })

  it('shows an unlinked plain label until the desk has loaded', () => {
    // Act
    const parent = sessionParent(deskHost, undefined, true)

    // Assert
    expect(parent).toEqual([
      { label: 'Desks', href: undefined },
      { label: 'Desk', href: undefined },
    ])
  })
})

describe('worksDesk', () => {
  const desk = (memberIds: string[], canManage = false) =>
    ({
      canManage,
      members: memberIds.map((id) => ({ employee: { id } })),
    }) as unknown as IntakeDeskDto

  it.each([
    ['a member of the desk', desk(['e-1']), 'e-1', true],
    ['someone who runs it without being a member', desk([], true), 'e-1', true],
    ['someone off the desk', desk(['e-2']), 'e-1', false],
    ['an account with no employee', desk(['e-2']), null, false],
    ['a desk that has not loaded', undefined, 'e-1', false],
  ])('is as expected for %s', (_who, target, employeeId, expected) => {
    // Act
    const result = worksDesk(target, employeeId)

    // Assert
    expect(result).toBe(expected)
  })
})

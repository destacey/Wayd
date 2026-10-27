import { render, screen } from '@testing-library/react'
import { IntakeDeskDto } from '@/src/services/wayd-api'
import { UnownedDeskAlert, describeWaiting, deskPhrase } from './desk-status'

describe('deskPhrase', () => {
  it.each([
    ['IT', 'the IT desk'],
    ['Product delivery desk', 'the Product delivery desk'],
    ['Security Desk', 'the Security Desk'],
  ])('names %s as %s', (name, expected) => {
    // Act
    const phrase = deskPhrase(name)

    // Assert
    expect(phrase).toBe(expected)
  })
})

const desk = (overrides: Partial<IntakeDeskDto> = {}) =>
  ({
    id: 'd-1',
    key: 1,
    name: 'IT',
    members: [],
    ...overrides,
  }) as IntakeDeskDto

describe('describeWaiting', () => {
  it('says nothing is waiting when the count is zero', () => {
    // Act
    const text = describeWaiting({ count: 0 })

    // Assert
    expect(text).toBe('Nothing waiting')
  })

  it('names the count and how long the oldest has waited', () => {
    // Arrange
    const twoWeeksAgo = new Date(Date.now() - 15 * 24 * 60 * 60 * 1000)

    // Act
    const text = describeWaiting({ count: 3, oldestSubmittedOn: twoWeeksAgo })

    // Assert
    expect(text).toBe('3 waiting, the oldest submitted 2 weeks ago')
  })
})

describe('UnownedDeskAlert', () => {
  it('warns about a desk with no members and says what is waiting where the server sent it', () => {
    // Arrange / Act
    render(<UnownedDeskAlert desk={desk({ waiting: { count: 4 } })} />)

    // Assert
    expect(screen.getByText('Nobody works this desk')).toBeInTheDocument()
    expect(screen.getByText(/^4 waiting/)).toBeInTheDocument()
  })

  it('never states a count the reader was not sent', () => {
    // Arrange / Act
    render(<UnownedDeskAlert desk={desk({ waiting: undefined })} />)

    // Assert
    expect(screen.getByText('Nobody works this desk')).toBeInTheDocument()
    expect(screen.queryByText(/waiting/)).not.toBeInTheDocument()
  })

  it('stays silent for a desk with members', () => {
    // Arrange / Act
    const { container } = render(
      <UnownedDeskAlert
        desk={desk({
          members: [
            {
              employee: { id: 'e-1', key: 1, name: 'Avery' },
              role: 'Agent' as any,
            },
          ],
        })}
      />,
    )

    // Assert
    expect(container).toBeEmptyDOMElement()
  })
})

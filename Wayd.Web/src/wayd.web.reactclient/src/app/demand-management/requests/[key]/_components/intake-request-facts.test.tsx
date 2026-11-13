import { render, screen } from '@testing-library/react'
import {
  DeliverySignal,
  IntakeRequestDetailsDto,
  StatusCategory,
} from '@/src/services/wayd-api'
import IntakeRequestFacts from './intake-request-facts'

// Linking a person checks the reader's permissions, which these tests are not about.
vi.mock('../../../_components/employee-name', () => ({
  default: ({ employee }: { employee: { name: string } }) => employee.name,
}))

let mockPokerEnabled = false
vi.mock('@/src/hooks', () => ({
  useFeatureFlag: () => ({ isEnabled: mockPokerEnabled, isLoading: false }),
}))

vi.mock('@/src/components/contexts/auth', () => ({
  default: () => ({ hasPermissionClaim: () => true }),
}))

vi.mock('./intake-request-poker-rounds', () => ({
  default: () => <div data-testid="poker-rounds" />,
}))

const substance = (overrides: Record<string, unknown> = {}) => ({
  description: 'Export invoices monthly',
  submittedBy: { id: 'e-1', key: 1, name: 'Riley Requester' },
  participants: [],
  ...overrides,
})

const request = (
  category: StatusCategory,
  deliveryState?: DeliverySignal,
): IntakeRequestDetailsDto =>
  ({
    summary: {
      id: 'r-42',
      key: 42,
      status: { id: 's-1', name: 'Accepted', category },
      desk: { id: 'd-1', key: 1, name: 'Finance' },
      requester: { id: 'e-1', key: 1, name: 'Riley Requester' },
    },
    substance: undefined,
    estimateValues: [],
    deliveryState,
  }) as unknown as IntakeRequestDetailsDto

describe('IntakeRequestFacts', () => {
  beforeEach(() => {
    mockPokerEnabled = false
  })

  it('shows the card a poker round settled on in place of an estimate', () => {
    // Arrange
    const withCard = {
      ...request(StatusCategory.Active),
      substance: substance({ estimateCard: '?' }),
    } as unknown as IntakeRequestDetailsDto

    // Act
    render(<IntakeRequestFacts request={withCard} />)

    // Assert
    expect(screen.getByText('Estimate')).toBeInTheDocument()
    expect(screen.getByText('?')).toBeInTheDocument()
    expect(screen.getByText('Not estimated')).toBeInTheDocument()
  })

  it('lists the poker rounds for a reader of the substance who can open Planning Poker', () => {
    // Arrange
    mockPokerEnabled = true
    const readable = {
      ...request(StatusCategory.Active),
      substance: substance(),
    } as unknown as IntakeRequestDetailsDto

    // Act
    render(<IntakeRequestFacts request={readable} />)

    // Assert
    expect(screen.getByTestId('poker-rounds')).toBeInTheDocument()
  })

  it.each([
    ['Planning Poker is off', false, true],
    ['the substance is withheld', true, false],
  ])('leaves the poker rounds out when %s', (_why, pokerEnabled, hasSubstance) => {
    // Arrange
    mockPokerEnabled = pokerEnabled
    const shown = {
      ...request(StatusCategory.Active),
      substance: hasSubstance ? substance() : undefined,
    } as unknown as IntakeRequestDetailsDto

    // Act
    render(<IntakeRequestFacts request={shown} />)

    // Assert
    expect(screen.queryByTestId('poker-rounds')).not.toBeInTheDocument()
  })

  it('shows the request’s delivery state', () => {
    // Arrange / Act
    render(
      <IntakeRequestFacts
        request={request(StatusCategory.Active, DeliverySignal.InProgress)}
      />,
    )

    // Assert
    expect(screen.getByText('Delivery')).toBeInTheDocument()
    expect(screen.getByText('In Progress')).toBeInTheDocument()
  })

  it('leaves delivery out when no outcome reports one', () => {
    // Arrange / Act
    render(<IntakeRequestFacts request={request(StatusCategory.Active)} />)

    // Assert
    expect(screen.queryByText('Delivery')).not.toBeInTheDocument()
  })

  it('leaves delivery out of a declined, withdrawn or duplicate request', () => {
    // Arrange / Act
    render(
      <IntakeRequestFacts
        request={request(StatusCategory.Removed, DeliverySignal.InProgress)}
      />,
    )

    // Assert
    expect(screen.queryByText('Delivery')).not.toBeInTheDocument()
    expect(screen.queryByText('In Progress')).not.toBeInTheDocument()
  })

  it('shows delivery on a finished request', () => {
    // Arrange / Act
    render(
      <IntakeRequestFacts
        request={request(StatusCategory.Done, DeliverySignal.Done)}
      />,
    )

    // Assert
    expect(screen.getByText('Delivery')).toBeInTheDocument()
    expect(screen.getByText('Done')).toBeInTheDocument()
  })
})

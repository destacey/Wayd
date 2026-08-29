import { render, screen } from '@testing-library/react'
import { FeatureFlagDto } from '@/src/services/wayd-api'
import FeatureFlagPanel from './feature-flag-panel'

const featureFlag = (
  overrides: Partial<FeatureFlagDto> = {},
): FeatureFlagDto =>
  ({
    id: 7,
    name: 'planning-poker',
    displayName: 'Planning Poker',
    description: 'Estimation sessions for planning teams',
    isEnabled: true,
    isArchived: false,
    isSystem: false,
    created: new Date('2026-01-05T10:00:00Z'),
    lastModified: new Date('2026-02-01T10:00:00Z'),
    ...overrides,
  }) as FeatureFlagDto

/** The value rendered under a given label. */
const valueFor = (label: string) =>
  screen.getByText(label).parentElement?.textContent?.replace(label, '')

describe('FeatureFlagPanel', () => {
  it('renders the record fields', () => {
    // Arrange / Act
    render(<FeatureFlagPanel featureFlag={featureFlag()} />)

    // Assert
    expect(valueFor('Enabled')).toBe('Yes')
    expect(valueFor('Type')).toBe('User')
    expect(valueFor('Description')).toBe(
      'Estimation sessions for planning teams',
    )
  })

  it('keeps the flag name, which the code gates on', () => {
    // Arrange / Act — the list shows displayName, but someone reading this
    // panel is usually about to search for the raw name.
    render(<FeatureFlagPanel featureFlag={featureFlag()} />)

    // Assert
    expect(valueFor('Name')).toBe('planning-poker')
  })

  it('names a system flag as such', () => {
    // Arrange / Act
    render(<FeatureFlagPanel featureFlag={featureFlag({ isSystem: true })} />)

    // Assert
    expect(valueFor('Type')).toBe('System')
  })

  it('shows a disabled flag as No', () => {
    // Arrange / Act
    render(<FeatureFlagPanel featureFlag={featureFlag({ isEnabled: false })} />)

    // Assert
    expect(valueFor('Enabled')).toBe('No')
  })

  it('flags an archived record', () => {
    // Arrange / Act
    render(<FeatureFlagPanel featureFlag={featureFlag({ isArchived: true })} />)

    // Assert
    expect(screen.getByText('Status')).toBeInTheDocument()
    expect(screen.getByText('Archived')).toBeInTheDocument()
  })

  it('says nothing about archiving on a live flag', () => {
    // Arrange / Act — the row is only worth the space when it is true
    render(<FeatureFlagPanel featureFlag={featureFlag()} />)

    // Assert
    expect(screen.queryByText('Status')).not.toBeInTheDocument()
  })

  it('omits an absent description', () => {
    // Arrange / Act
    render(
      <FeatureFlagPanel featureFlag={featureFlag({ description: undefined })} />,
    )

    // Assert
    expect(screen.queryByText('Description')).not.toBeInTheDocument()
  })

  it('renders nothing while no record is selected', () => {
    // Arrange / Act
    const { container } = render(<FeatureFlagPanel featureFlag={undefined} />)

    // Assert
    expect(container).toBeEmptyDOMElement()
  })
})

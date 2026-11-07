import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { FeatureFlagModule } from '@/src/services/wayd-api'
import FeatureFlagModuleCard from './feature-flag-module-card'
import { flagDto, moduleDto } from './test-data'

const productModule = moduleDto(
  FeatureFlagModule.ProductManagement,
  'Product Management',
  4,
)
const productSwitch = flagDto({
  id: 1,
  name: 'product-management',
  displayName: 'Product Management',
  description: 'The Product Management module.',
  module: FeatureFlagModule.ProductManagement,
  isModuleSwitch: true,
  isEnabled: true,
})
const incidents = flagDto({
  id: 2,
  name: 'incident-management',
  displayName: 'Incident Management',
  description: 'Severity scales and incident records.',
  module: FeatureFlagModule.ProductManagement,
  isEnabled: true,
})

describe('FeatureFlagModuleCard', () => {
  it('shows each flag with its key, description and switch', () => {
    // Arrange / Act
    render(
      <FeatureFlagModuleCard
        group={{
          module: productModule,
          moduleSwitch: productSwitch,
          flags: [incidents],
        }}
        canUpdate
        onToggle={jest.fn()}
      />,
    )

    // Assert
    expect(
      screen.getByText('The Product Management module.'),
    ).toBeInTheDocument()
    expect(screen.getByText('product-management')).toBeInTheDocument()
    expect(screen.getByText('incident-management')).toBeInTheDocument()
    expect(
      screen.getByText('Severity scales and incident records.'),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('switch', { name: 'Incident Management' }),
    ).toBeChecked()
    expect(
      screen.getByRole('switch', { name: 'Product Management module' }),
    ).toBeChecked()
  })

  it('passes the flag and its new state to onToggle', async () => {
    // Arrange
    const onToggle = jest.fn()
    render(
      <FeatureFlagModuleCard
        group={{
          module: productModule,
          moduleSwitch: productSwitch,
          flags: [incidents],
        }}
        canUpdate
        onToggle={onToggle}
      />,
    )

    // Act
    await userEvent.click(
      screen.getByRole('switch', { name: 'Product Management module' }),
    )

    // Assert
    expect(onToggle).toHaveBeenCalledWith(productSwitch, false)
  })

  it('disables the module flags while the module switch is off', () => {
    // Arrange / Act
    render(
      <FeatureFlagModuleCard
        group={{
          module: productModule,
          moduleSwitch: { ...productSwitch, isEnabled: false },
          flags: [incidents],
        }}
        canUpdate
        onToggle={jest.fn()}
      />,
    )

    // Assert
    expect(
      screen.getByRole('switch', { name: 'Incident Management' }),
    ).toBeDisabled()
    expect(
      screen.getByRole('switch', { name: 'Product Management module' }),
    ).toBeEnabled()
    expect(
      screen.getByRole('switch', { name: 'Incident Management' }),
    ).toHaveAccessibleDescription(
      'Turn on Product Management to use these features.',
    )
  })

  it('shows no module-off note when the module has only its switch', () => {
    // Arrange / Act
    render(
      <FeatureFlagModuleCard
        group={{
          module: productModule,
          moduleSwitch: { ...productSwitch, isEnabled: false },
          flags: [],
        }}
        canUpdate
        onToggle={jest.fn()}
      />,
    )

    // Assert
    expect(screen.queryByText(/to use these features/)).not.toBeInTheDocument()
  })

  it('disables every switch without the update permission', () => {
    // Arrange / Act
    render(
      <FeatureFlagModuleCard
        group={{
          module: productModule,
          moduleSwitch: productSwitch,
          flags: [incidents],
        }}
        canUpdate={false}
        onToggle={jest.fn()}
      />,
    )

    // Assert
    screen
      .getAllByRole('switch')
      .forEach((toggle) => expect(toggle).toBeDisabled())
  })

  it('counts the flags that are on when the module has no switch', () => {
    // Arrange
    const planning = moduleDto(FeatureFlagModule.Planning, 'Planning', 2)

    // Act
    render(
      <FeatureFlagModuleCard
        group={{
          module: planning,
          flags: [
            flagDto({ id: 3, name: 'story-maps', isEnabled: true }),
            flagDto({ id: 4, name: 'planning-poker', isEnabled: false }),
          ],
        }}
        canUpdate
        onToggle={jest.fn()}
      />,
    )

    // Assert
    expect(screen.getByText('1 of 2 on')).toBeInTheDocument()
    expect(screen.getByText('Planning description')).toBeInTheDocument()
  })
})

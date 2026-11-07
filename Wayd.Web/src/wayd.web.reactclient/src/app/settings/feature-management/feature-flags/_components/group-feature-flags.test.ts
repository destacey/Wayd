import { FeatureFlagModule } from '@/src/services/wayd-api'
import { groupFeatureFlags } from './group-feature-flags'
import { flagDto, moduleDto } from './test-data'

const modules = [
  moduleDto(FeatureFlagModule.ProductManagement, 'Product Management', 4),
  moduleDto(FeatureFlagModule.Planning, 'Planning', 2),
  moduleDto(FeatureFlagModule.Platform, 'Platform', 1),
]

const productSwitch = flagDto({
  id: 1,
  name: 'product-management',
  displayName: 'Product Management',
  module: FeatureFlagModule.ProductManagement,
  isModuleSwitch: true,
})
const incidents = flagDto({
  id: 2,
  name: 'incident-management',
  displayName: 'Incident Management',
  description: 'Severity scales and incident records.',
  module: FeatureFlagModule.ProductManagement,
})
const storyMaps = flagDto({
  id: 3,
  name: 'story-maps',
  displayName: 'Story Maps',
})
const poker = flagDto({
  id: 4,
  name: 'planning-poker',
  displayName: 'planning Poker',
})

const flags = [productSwitch, incidents, storyMaps, poker]

describe('groupFeatureFlags', () => {
  it('orders modules, leaves out empty ones, and sorts flags by display name', () => {
    // Arrange / Act
    const groups = groupFeatureFlags(modules, flags)

    // Assert
    expect(groups.map((g) => g.module.name)).toEqual([
      'Planning',
      'Product Management',
    ])
    expect(groups[0].flags.map((f) => f.id)).toEqual([4, 3])
  })

  it('separates the module switch from the module flags', () => {
    // Arrange / Act
    const product = groupFeatureFlags(modules, flags)[1]

    // Assert
    expect(product.moduleSwitch).toBe(productSwitch)
    expect(product.flags).toEqual([incidents])
  })

  it('keeps flags whose description matches, with their module switch', () => {
    // Arrange / Act
    const groups = groupFeatureFlags(modules, flags, '  SEVERITY ')

    // Assert
    expect(groups).toHaveLength(1)
    expect(groups[0].moduleSwitch).toBe(productSwitch)
    expect(groups[0].flags).toEqual([incidents])
  })

  it('matches the display name', () => {
    // Arrange / Act
    const groups = groupFeatureFlags(modules, flags, 'story m')

    // Assert
    expect(groups.flatMap((g) => g.flags)).toEqual([storyMaps])
  })

  it('matches the flag key', () => {
    // Arrange / Act
    const groups = groupFeatureFlags(modules, flags, 'story-')

    // Assert
    expect(groups.flatMap((g) => g.flags)).toEqual([storyMaps])
  })

  it('keeps a module switch that matches on its own', () => {
    // Arrange
    const onlySwitch = [productSwitch, storyMaps]

    // Act
    const groups = groupFeatureFlags(modules, onlySwitch, 'product-man')

    // Assert
    expect(groups).toHaveLength(1)
    expect(groups[0].moduleSwitch).toBe(productSwitch)
    expect(groups[0].flags).toEqual([])
  })

  it('keeps every flag of a module whose name matches', () => {
    // Arrange / Act
    const groups = groupFeatureFlags(modules, flags, 'planning')

    // Assert
    expect(groups.map((g) => g.module.name)).toEqual(['Planning'])
    expect(groups[0].flags).toHaveLength(2)
  })

  it('returns nothing when no flag matches', () => {
    // Arrange / Act
    const groups = groupFeatureFlags(modules, flags, 'nothing-like-this')

    // Assert
    expect(groups).toEqual([])
  })
})

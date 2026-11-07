import {
  FeatureFlagDto,
  FeatureFlagModule,
  FeatureFlagModuleDto,
} from '@/src/services/wayd-api'

export const moduleDto = (
  code: FeatureFlagModule,
  name: string,
  order: number,
): FeatureFlagModuleDto => ({
  id: order,
  code,
  name,
  description: `${name} description`,
  order,
})

export const flagDto = (
  overrides: Partial<FeatureFlagDto> & Pick<FeatureFlagDto, 'id' | 'name'>,
): FeatureFlagDto => ({
  displayName: overrides.name,
  description: undefined,
  module: FeatureFlagModule.Planning,
  isModuleSwitch: false,
  isEnabled: false,
  ...overrides,
})

import {
  FeatureFlagDto,
  FeatureFlagModule,
  FeatureFlagModuleDto,
} from '@/src/services/api/wayd.schemas'

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

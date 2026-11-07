import type {
  FeatureFlagDto,
  FeatureFlagModuleDto,
} from '@/src/services/wayd-api'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'

/** One module's card on the Feature Flags page. */
export interface FeatureFlagModuleGroup {
  module: FeatureFlagModuleDto
  /** The flag that turns the whole module on or off, if the module has one. */
  moduleSwitch?: FeatureFlagDto
  /** The module's other flags, by display name. */
  flags: FeatureFlagDto[]
}

const contains = (text: string | undefined, term: string) =>
  !!text && text.toLowerCase().includes(term)

/**
 * Groups flags into their modules, in module order, leaving out modules with
 * no flags. A search term keeps a flag whose name, key or description holds
 * it; a term in the module's name keeps all of the module's flags. A module's
 * switch stays beside any of its flags that match, because those flags do
 * nothing while it is off.
 */
export const groupFeatureFlags = (
  modules: FeatureFlagModuleDto[],
  flags: FeatureFlagDto[],
  search = '',
): FeatureFlagModuleGroup[] => {
  const term = search.trim().toLowerCase()
  const flagMatches = (flag: FeatureFlagDto) =>
    contains(flag.displayName, term) ||
    contains(flag.name, term) ||
    contains(flag.description, term)

  return [...modules]
    .sort((a, b) => a.order - b.order)
    .map((module): FeatureFlagModuleGroup => {
      const inModule = flags.filter((f) => f.module === module.code)
      const moduleSwitch = inModule.find((f) => f.isModuleSwitch)
      const others = inModule
        .filter((f) => !f.isModuleSwitch)
        .sort((a, b) => caseInsensitiveCompare(a.displayName, b.displayName))

      if (!term || contains(module.name, term)) {
        return { module, moduleSwitch, flags: others }
      }

      const matching = others.filter(flagMatches)
      const keepSwitch =
        !!moduleSwitch && (matching.length > 0 || flagMatches(moduleSwitch))
      return {
        module,
        moduleSwitch: keepSwitch ? moduleSwitch : undefined,
        flags: matching,
      }
    })
    .filter((group) => group.moduleSwitch || group.flags.length > 0)
}

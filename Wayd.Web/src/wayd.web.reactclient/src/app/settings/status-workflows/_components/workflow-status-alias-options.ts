import { WorkflowAliasDto, WorkflowStatusDto } from '@/src/services/wayd-api'

/** The "no well-known meaning" option. The API models it as alias zero. */
export const NO_ALIAS = 0

export interface AliasOption {
  value: number
  label: string
}

/**
 * The alias choices open to one status.
 *
 * An alias is a promise that exactly one status carries a given meaning, so an
 * alias another status already holds is not offered — leaving it selectable
 * would let the user build a workflow the server refuses to save. The status
 * being edited keeps its own alias in the list, which is why `currentStatusId`
 * is excluded rather than every taken alias being dropped outright.
 *
 * "None" is always available: most statuses carry no well-known meaning.
 */
export const buildAliasOptions = (
  aliases: WorkflowAliasDto[] | undefined,
  statuses: WorkflowStatusDto[] | undefined,
  currentStatusId?: string,
): AliasOption[] => {
  const taken = new Set(
    (statuses ?? [])
      .filter((s) => s.id !== currentStatusId && s.alias !== NO_ALIAS)
      .map((s) => s.alias),
  )

  return [
    { value: NO_ALIAS, label: 'None' },
    ...(aliases ?? [])
      .filter((a) => a.value !== NO_ALIAS && !taken.has(a.value))
      .map((a) => ({
        value: a.value,
        label: a.isRequired ? `${a.name} (required)` : a.name,
      })),
  ]
}

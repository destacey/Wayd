import {
  StatusCategory,
  WorkflowAliasDto,
  WorkflowStatusDto,
} from '@/src/services/wayd-api'

/** The "no well-known meaning" option. The API models it as alias zero. */
export const NO_ALIAS = 0

export interface AliasOption {
  value: number
  label: string
}

/**
 * Whether a status in `category` may carry `alias`: always for no alias, and
 * otherwise only in the category the owner type ties that alias to.
 */
export const aliasFitsCategory = (
  aliases: WorkflowAliasDto[] | undefined,
  alias: number | undefined,
  category: StatusCategory | undefined,
): boolean =>
  alias === undefined ||
  alias === NO_ALIAS ||
  (aliases ?? []).some((a) => a.value === alias && a.category === category)

/**
 * The alias choices open to one status in `category`.
 *
 * Each alias belongs to one category, and the server refuses any other pairing,
 * so only the aliases of the chosen category are offered — none until a
 * category is chosen. An alias is also a promise that exactly one status
 * carries a given meaning, so one another status already holds is not offered.
 * The status being edited keeps its own alias in the list, which is why
 * `currentStatusId` is excluded rather than every taken alias being dropped
 * outright. It keeps it even when the alias belongs under another category —
 * possible on a draft cloned from a workflow saved before aliases were tied to
 * categories — labelled with where it belongs, so the select names the
 * meaning the publish dialog asks to be moved rather than showing a number. An
 * alias the owner type no longer declares is kept the same way, as an unknown
 * meaning, so it can be seen and cleared.
 *
 * "None" is always available: most statuses carry no well-known meaning.
 */
export const buildAliasOptions = (
  aliases: WorkflowAliasDto[] | undefined,
  statuses: WorkflowStatusDto[] | undefined,
  category: StatusCategory | undefined,
  currentStatusId?: string,
): AliasOption[] => {
  const taken = new Set(
    (statuses ?? [])
      .filter((s) => s.id !== currentStatusId && s.alias !== NO_ALIAS)
      .map((s) => s.alias),
  )
  const ownAlias = (statuses ?? []).find((s) => s.id === currentStatusId)?.alias
  const ownAliasIsUnknown =
    ownAlias !== undefined &&
    ownAlias !== NO_ALIAS &&
    !(aliases ?? []).some((a) => a.value === ownAlias)

  return [
    { value: NO_ALIAS, label: 'None' },
    ...(aliases ?? [])
      .filter(
        (a) =>
          a.value !== NO_ALIAS &&
          (a.category === category || a.value === ownAlias) &&
          !taken.has(a.value),
      )
      .map((a) => ({
        value: a.value,
        label:
          a.category !== category
            ? `${a.name} (belongs under ${a.category})`
            : a.isRequired
              ? `${a.name} (required)`
              : a.name,
      })),
    ...(ownAliasIsUnknown
      ? [{ value: ownAlias, label: `Unknown meaning (${ownAlias})` }]
      : []),
  ]
}

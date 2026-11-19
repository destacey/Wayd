import type { FilterOption } from './types'

/**
 * Set filter options for a column on a closed set, from the generated client's
 * enum object (`ProductStatusAlias`): each value is a code, which a server-mode
 * list's filter matches as it is, so the column needs no request for its values.
 *
 * @param codes The generated enum object, mapping each name to its code.
 * @param labels What to show for a code where it differs from the code itself
 *   (`Resolved` shown as "Recovered").
 * @param only The codes to offer, in this order; every code, in the enum's
 *   order, when omitted.
 */
export const enumFilterOptions = <TCode extends string>(
  codes: Readonly<Record<string, TCode>>,
  labels?: Partial<Record<TCode, string>>,
  only?: readonly TCode[],
): FilterOption[] =>
  (only ?? Object.values(codes)).map((code) => ({
    value: code,
    label: labels?.[code] ?? code,
  }))

import {
  IntakeRequestCurrentEstimateDto,
  IntakeRequestEstimateDto,
  IntakeRequestSummaryDto,
} from '@/src/services/wayd-api'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'

/** Each request's estimate by request id, for the requests whose estimate the reader may read. */
export type RequestEstimates = Map<string, IntakeRequestEstimateDto>

export const toRequestEstimates = (
  estimates: IntakeRequestCurrentEstimateDto[] | undefined,
): RequestEstimates =>
  new Map((estimates ?? []).map((e) => [e.requestId, e.estimate]))

/**
 * Smallest first, among estimates on the same scale version: a position means nothing beside another
 * version's, since a later version can add values and shift them. Estimates on different versions sort in
 * separate runs, by scale name and then version number, and a missing estimate sorts after every other.
 * Returns 0 for two estimates of the same size, so the caller decides the tie.
 */
export const compareEstimates = (
  a: IntakeRequestEstimateDto | undefined,
  b: IntakeRequestEstimateDto | undefined,
): number => {
  if (!a || !b) return a ? -1 : b ? 1 : 0
  if (a.scale.versionId !== b.scale.versionId) {
    const byScale = caseInsensitiveCompare(a.scale.name, b.scale.name)
    if (byScale !== 0) return byScale
    if (a.scale.key !== b.scale.key) return a.scale.key - b.scale.key
    return a.scale.versionNumber - b.scale.versionNumber
  }
  return a.position - b.position
}

/**
 * Largest estimate first, within each scale version's run, so the biggest asks on the desk come to the
 * top. Requests with no estimate, or whose estimate is withheld, come last. Ties keep the newest request
 * first.
 */
export const compareByEstimate =
  (estimates: RequestEstimates) =>
  (a: IntakeRequestSummaryDto, b: IntakeRequestSummaryDto): number => {
    const ea = estimates.get(a.id)
    const eb = estimates.get(b.id)
    if (!ea || !eb) {
      const byPresence = (ea ? 0 : 1) - (eb ? 0 : 1)
      return byPresence !== 0 ? byPresence : b.key - a.key
    }
    if (ea.scale.versionId !== eb.scale.versionId)
      return compareEstimates(ea, eb)
    const bySize = eb.position - ea.position
    return bySize !== 0 ? bySize : b.key - a.key
  }

/** What scale an estimate was given on, as a tooltip names it. */
export const estimateScaleDescription = (
  estimate: IntakeRequestEstimateDto,
): string =>
  `Estimated on ${estimate.scale.name}, version ${estimate.scale.versionNumber}${estimate.scale.isCurrentVersion ? '' : ', which the scale has since replaced'}`

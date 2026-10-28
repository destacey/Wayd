import {
  IntakeRequestCurrentScoreDto,
  IntakeRequestSummaryDto,
} from '@/src/services/wayd-api'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import type { RequestOrder } from '../requests/_components/request-filters'

/** Each request's current score by request id, for the requests whose score the reader may read. */
export type RequestScores = Map<string, IntakeRequestCurrentScoreDto>

export const toRequestScores = (
  scores: IntakeRequestCurrentScoreDto[] | undefined,
): RequestScores => new Map((scores ?? []).map((s) => [s.requestId, s]))

/**
 * Highest score first, among scores from the same model: values from two models are on different scales, so
 * desks that score with different models rank in separate runs, one per model by its name. A score from a
 * model its desk no longer uses ranks after every comparable score, and a request with no score, or whose
 * score is withheld, after both. Ties keep the newest request first.
 */
export const compareByScore =
  (scores: RequestScores) =>
  (a: IntakeRequestSummaryDto, b: IntakeRequestSummaryDto): number => {
    const tier = (r: IntakeRequestSummaryDto) => {
      const score = scores.get(r.id)
      return !score ? 2 : score.isDeskModel ? 0 : 1
    }
    const byTier = tier(a) - tier(b)
    if (byTier !== 0) return byTier
    const modelA = scores.get(a.id)?.scoringModel
    const modelB = scores.get(b.id)?.scoringModel
    if (modelA && modelB && modelA.id !== modelB.id) {
      const byModel = caseInsensitiveCompare(modelA.name, modelB.name)
      return byModel !== 0 ? byModel : modelA.key - modelB.key
    }
    const byValue =
      (scores.get(b.id)?.value ?? 0) - (scores.get(a.id)?.value ?? 0)
    return byValue !== 0 ? byValue : b.key - a.key
  }

/** The requests in the order chosen; the usual order leaves them as they are. */
export const orderRequests = (
  requests: IntakeRequestSummaryDto[],
  order: RequestOrder,
  scores: RequestScores,
): IntakeRequestSummaryDto[] =>
  order === 'score' ? [...requests].sort(compareByScore(scores)) : requests

import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import { DependencyHealth } from '@/src/components/types'
import { DependencyDto } from '@/src/services/wayd-api'

/** The partner key for work items with no team; every other partner is keyed by team id. */
export const NO_TEAM_PARTNER_KEY = 'no-team'

/** How many dependencies have each health. */
export type PartnerHealthCounts = Record<DependencyHealth, number>

/** The dependencies one team shares with another, split by which side waits. */
export interface DependencyPartner {
  key: string
  name: string
  /** The team code, or "No team". */
  label: string
  /** Dependencies where the team waits on this partner: the partner owns the predecessor. */
  waitingOn: PartnerHealthCounts
  /** Dependencies where this partner waits on the team: the team owns the predecessor. */
  waitedOnBy: PartnerHealthCounts
}

export interface DependencyPartnersData {
  /** Most dependencies first, then by label, with "No team" last. */
  partners: DependencyPartner[]
  /** Dependencies with both ends on the team, which have no partner. */
  withinTeam: number
}

const noHealth = (): PartnerHealthCounts => ({
  [DependencyHealth.Healthy]: 0,
  [DependencyHealth.AtRisk]: 0,
  [DependencyHealth.Unhealthy]: 0,
  [DependencyHealth.Unknown]: 0,
})

/** The sum of a set of health counts. */
export const totalOf = (counts: PartnerHealthCounts) =>
  Object.values(counts).reduce((sum, n) => sum + n, 0)

const partnerKey = (team: { id: string } | undefined) =>
  team?.id ?? NO_TEAM_PARTNER_KEY

/** Groups a team's dependencies by the other team in each, and by which side waits. */
export function buildDependencyPartnersData(
  teamId: string,
  dependencies: DependencyDto[],
): DependencyPartnersData {
  const partners = new Map<string, DependencyPartner>()
  let withinTeam = 0

  for (const dependency of dependencies) {
    const sourceIsTeam = dependency.source.team?.id === teamId
    const targetIsTeam = dependency.target.team?.id === teamId
    if (sourceIsTeam && targetIsTeam) {
      withinTeam++
      continue
    }
    if (!sourceIsTeam && !targetIsTeam) continue

    const other = sourceIsTeam ? dependency.target.team : dependency.source.team
    const key = partnerKey(other)
    let partner = partners.get(key)
    if (!partner) {
      partner = {
        key,
        name: other?.name ?? 'No team',
        label: other?.code ?? 'No team',
        waitingOn: noHealth(),
        waitedOnBy: noHealth(),
      }
      partners.set(key, partner)
    }

    const health = dependency.health.id as DependencyHealth
    if (sourceIsTeam) partner.waitedOnBy[health]++
    else partner.waitingOn[health]++
  }

  const size = (p: DependencyPartner) =>
    totalOf(p.waitingOn) + totalOf(p.waitedOnBy)

  return {
    partners: [...partners.values()].sort((a, b) => {
      if (a.key === NO_TEAM_PARTNER_KEY) return 1
      if (b.key === NO_TEAM_PARTNER_KEY) return -1
      return size(b) - size(a) || caseInsensitiveCompare(a.label, b.label)
    }),
    withinTeam,
  }
}

/** Whether a dependency involves the picked partner; nothing picked matches everything. */
export function matchesDependencyPartner(
  dependency: DependencyDto,
  teamId: string,
  partner: string | null,
): boolean {
  if (!partner) return true
  const sourceIsTeam = dependency.source.team?.id === teamId
  const targetIsTeam = dependency.target.team?.id === teamId
  if (sourceIsTeam === targetIsTeam) return false
  const other = sourceIsTeam ? dependency.target.team : dependency.source.team
  return partnerKey(other) === partner
}

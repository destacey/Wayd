import { DependencyHealth } from '@/src/components/types'
import { DependencyDto } from '@/src/services/wayd-api'
import {
  buildDependencyPartnersData,
  matchesDependencyPartner,
  NO_TEAM_PARTNER_KEY,
} from './dependency-partners-data'

const team = (id: string, name: string, code: string) => ({
  id,
  key: 1,
  name,
  code,
  type: 'Team',
})

const us = team('t-us', 'Us', 'US')
const pulsar = team('t-pulsar', 'Pulsar', 'PLS')
const quasar = team('t-quasar', 'Quasar', 'QSR')

const healthNames: Record<DependencyHealth, string> = {
  [DependencyHealth.Healthy]: 'Healthy',
  [DependencyHealth.AtRisk]: 'At Risk',
  [DependencyHealth.Unhealthy]: 'Unhealthy',
  [DependencyHealth.Unknown]: 'Unknown',
}

let nextId = 1
const dependency = (
  sourceTeam: ReturnType<typeof team> | undefined,
  targetTeam: ReturnType<typeof team> | undefined,
  health: DependencyHealth = DependencyHealth.Healthy,
): DependencyDto => {
  const item = (t: ReturnType<typeof team> | undefined) =>
    ({ id: `w${nextId++}`, team: t }) as DependencyDto['source']
  return {
    id: `d${nextId++}`,
    source: item(sourceTeam),
    target: item(targetTeam),
    health: { id: health, name: healthNames[health] },
  } as DependencyDto
}

describe('buildDependencyPartnersData', () => {
  it('splits each partner by which side waits, counting health', () => {
    // Arrange
    const dependencies = [
      dependency(pulsar, us, DependencyHealth.Unhealthy), // we wait on Pulsar
      dependency(pulsar, us, DependencyHealth.Healthy), // we wait on Pulsar
      dependency(us, pulsar, DependencyHealth.AtRisk), // Pulsar waits on us
    ]

    // Act
    const data = buildDependencyPartnersData(us.id, dependencies)

    // Assert
    expect(data.partners).toHaveLength(1)
    expect(data.partners[0]).toEqual(
      expect.objectContaining({
        key: pulsar.id,
        label: 'PLS',
        waitingOn: expect.objectContaining({
          [DependencyHealth.Unhealthy]: 1,
          [DependencyHealth.Healthy]: 1,
          [DependencyHealth.AtRisk]: 0,
        }),
        waitedOnBy: expect.objectContaining({
          [DependencyHealth.AtRisk]: 1,
          [DependencyHealth.Healthy]: 0,
        }),
      }),
    )
  })

  it('orders partners by size, with No team last, and counts dependencies within the team', () => {
    // Arrange
    const dependencies = [
      dependency(us, undefined),
      dependency(us, undefined),
      dependency(us, undefined),
      dependency(us, pulsar),
      dependency(quasar, us),
      dependency(us, quasar),
      dependency(us, us),
    ]

    // Act
    const data = buildDependencyPartnersData(us.id, dependencies)

    // Assert
    expect(data.partners.map((p) => p.key)).toEqual([
      quasar.id,
      pulsar.id,
      NO_TEAM_PARTNER_KEY,
    ])
    expect(data.withinTeam).toBe(1)
  })

  it('ignores a dependency the team takes no part in', () => {
    // Act
    const data = buildDependencyPartnersData(us.id, [
      dependency(pulsar, quasar),
    ])

    // Assert
    expect(data.partners).toEqual([])
    expect(data.withinTeam).toBe(0)
  })
})

describe('matchesDependencyPartner', () => {
  const weWait = dependency(pulsar, us)
  const theyWait = dependency(us, pulsar)
  const other = dependency(us, quasar)
  const within = dependency(us, us)

  it('matches a picked partner in either direction', () => {
    // Act
    const matches = [weWait, theyWait, other, within].map((d) =>
      matchesDependencyPartner(d, us.id, pulsar.id),
    )

    // Assert
    expect(matches).toEqual([true, true, false, false])
  })

  it('matches everything when nothing is picked', () => {
    // Act / Assert
    expect(
      [weWait, theyWait, other, within].every((d) =>
        matchesDependencyPartner(d, us.id, null),
      ),
    ).toBe(true)
  })
})

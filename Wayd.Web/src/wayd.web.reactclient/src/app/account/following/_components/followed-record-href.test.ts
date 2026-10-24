import { FollowedRecordDto, LifecycleCategory } from '@/src/services/wayd-api'
import { followedRecordHref } from './followed-record-href'

const record = (overrides: Partial<FollowedRecordDto>): FollowedRecordDto => ({
  ownerType: 'ppm.project',
  id: 'id-1',
  key: 'APOLLO',
  name: 'Moon landing',
  statusName: 'Active',
  lifecycle: LifecycleCategory.Active,
  followedOn: new Date('2026-10-23T09:30:00Z'),
  ...overrides,
})

describe('followedRecordHref', () => {
  it.each([
    ['ppm.project', 'APOLLO', undefined, '/ppm/projects/APOLLO'],
    ['ppm.program', '42', undefined, '/ppm/programs/42'],
    [
      'planning.planning-interval-objective',
      '7',
      '26',
      '/planning/planning-intervals/26/objectives/7',
    ],
  ])('links a %s to its own page', (ownerType, key, parentKey, expected) => {
    // Act & Assert
    expect(followedRecordHref(record({ ownerType, key, parentKey }))).toBe(
      expected,
    )
  })

  it('gives no link for an objective without its planning interval', () => {
    // Act & Assert
    expect(
      followedRecordHref(
        record({
          ownerType: 'planning.planning-interval-objective',
          key: '7',
          parentKey: undefined,
        }),
      ),
    ).toBeNull()
  })

  it('gives no link for an owner type the client has no page for', () => {
    // Act & Assert
    expect(
      followedRecordHref(record({ ownerType: 'demand.intake-request' })),
    ).toBeNull()
  })
})

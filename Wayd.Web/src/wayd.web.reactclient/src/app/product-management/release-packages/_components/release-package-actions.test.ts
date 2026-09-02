import {
  ManifestEntryKind,
  ReleasePackageDto,
  StatusCategory,
} from '@/src/services/wayd-api'
import { releasePackageActionAvailability } from './release-package-actions'

const component = () => ({
  product: { id: 'p', key: 1, name: 'Checkout' },
  version: '1.0',
  kind: ManifestEntryKind.Changed,
})

const releasePackage = (
  overrides: Partial<ReleasePackageDto> = {},
): ReleasePackageDto =>
  ({
    id: '11111111-1111-1111-1111-111111111111',
    key: 1,
    version: '2026.04',
    status: {
      id: 's',
      name: 'Assembled',
      category: StatusCategory.Active,
      alias: 0,
    },
    components: [component()],
    ...overrides,
  }) as ReleasePackageDto

const assembled = () => releasePackage()

const released = () =>
  releasePackage({
    releasedDate: '2026-04-02' as unknown as Date,
    status: {
      id: 's',
      name: 'Released',
      category: StatusCategory.Done,
      alias: 11,
    },
  })

const withdrawn = () =>
  releasePackage({
    status: {
      id: 's',
      name: 'Withdrawn',
      category: StatusCategory.Removed,
      alias: 12,
    },
  })

describe('releasePackageActionAvailability', () => {
  it('offers every move on an assembled package', () => {
    // Arrange / Act
    const available = releasePackageActionAvailability(assembled())

    // Assert
    expect(available).toEqual({
      canEditManifest: true,
      canRelease: true,
      canWithdraw: true,
    })
  })

  it('closes the manifest once the package has shipped', () => {
    // Arrange / Act
    const available = releasePackageActionAvailability(released())

    // Assert — what was in the box is a matter of record afterwards.
    expect(available.canEditManifest).toBe(false)
    expect(available.canRelease).toBe(false)
  })

  it('closes the manifest on a withdrawn package', () => {
    // Arrange / Act
    const available = releasePackageActionAvailability(withdrawn())

    // Assert — a pulled package is kept because deployments reference it, not so it can be rewritten.
    expect(available.canEditManifest).toBe(false)
    expect(available.canRelease).toBe(false)
  })

  it('refuses withdrawing a package twice', () => {
    // Arrange / Act / Assert
    expect(releasePackageActionAvailability(withdrawn()).canWithdraw).toBe(false)
  })

  it('still offers withdrawal after release', () => {
    // Arrange / Act
    const available = releasePackageActionAvailability(released())

    // Assert — pulling something after it shipped is the case withdrawal exists for.
    expect(available.canWithdraw).toBe(true)
  })

  it('refuses releasing a package with an empty manifest', () => {
    // Arrange — the domain refuses this, so offering it would only produce a failed submit.
    const empty = releasePackage({ components: [] })

    // Act
    const available = releasePackageActionAvailability(empty)

    // Assert
    expect(available.canRelease).toBe(false)
    expect(available.canEditManifest).toBe(true)
  })
})

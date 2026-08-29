import { ConnectionDetailsDto } from '@/src/services/wayd-api'
import { DETAIL_REGISTRY, getDetailEntry } from './detail-registry'

const connection = (connectorName: string) =>
  ({
    id: 'c0ffee00-0000-0000-0000-000000000001',
    name: 'Acme AzDO',
    isActive: true,
    connector: { id: 1, name: connectorName },
  }) as ConnectionDetailsDto

describe('detail registry', () => {
  describe('resolution', () => {
    it.each([
      ['Azure DevOps'],
      ['Azure OpenAI'],
      ['Entra'],
      ['Workday'],
    ])('resolves the %s connector', (connectorName) => {
      // Arrange / Act
      const entry = getDetailEntry(connection(connectorName))

      // Assert — every connector must at least render its own config
      expect(entry).toBeDefined()
      expect(entry!.Details).toBeDefined()
    })

    it('returns nothing for a connector the UI does not know', () => {
      // Arrange / Act — a new connector can ship on the API before its UI
      // registration lands; the page shows a warning rather than a blank.
      const entry = getDetailEntry(connection('Some Future Connector'))

      // Assert
      expect(entry).toBeUndefined()
    })

    it('returns nothing when the connection has no connector', () => {
      // Arrange / Act
      const entry = getDetailEntry({ id: 'x', name: 'y' } as ConnectionDetailsDto)

      // Assert
      expect(entry).toBeUndefined()
    })

    it('returns nothing while the connection is still loading', () => {
      // Arrange / Act
      expect(getDetailEntry(undefined)).toBeUndefined()
    })
  })

  describe('section definitions', () => {
    it('gives every extra section a stable kebab-case key', () => {
      // Arrange — section keys reach the URL as `?section=`, so they are a
      // public contract and must be URL-safe.
      const keys = Object.values(DETAIL_REGISTRY).flatMap((entry) =>
        (entry?.extraSections ?? []).map((s) => s.key),
      )

      // Assert
      expect(keys.length).toBeGreaterThan(0)
      keys.forEach((key) => expect(key).toMatch(/^[a-z0-9]+(-[a-z0-9]+)*$/))
    })

    it('gives every extra section a label and a renderer', () => {
      // Arrange / Act
      const sections = Object.values(DETAIL_REGISTRY).flatMap(
        (entry) => entry?.extraSections ?? [],
      )

      // Assert
      sections.forEach((section) => {
        expect(section.label).toBeTruthy()
        expect(typeof section.render).toBe('function')
      })
    })

    it('never collides with the Overview section id', () => {
      // Arrange — the page reserves `overview` for the connector's own config,
      // so a registered section of that name would shadow it.
      const keys = Object.values(DETAIL_REGISTRY).flatMap((entry) =>
        (entry?.extraSections ?? []).map((s) => s.key),
      )

      // Assert
      expect(keys).not.toContain('overview')
    })

    it('keeps section keys unique within a connector', () => {
      // Arrange / Act — a duplicate would make one section unreachable, since
      // the page resolves by find()
      Object.values(DETAIL_REGISTRY).forEach((entry) => {
        const keys = (entry?.extraSections ?? []).map((s) => s.key)

        // Assert
        expect(new Set(keys).size).toBe(keys.length)
      })
    })
  })

  describe('per-connector capabilities', () => {
    it('keeps Azure DevOps its wrapper, actions, external link and sections', () => {
      // Arrange / Act — AzDO is the only connector using every hook the
      // registry offers, so it is the one that catches a dropped capability.
      const entry = getDetailEntry(connection('Azure DevOps'))!

      // Assert
      expect(entry.Wrapper).toBeDefined()
      expect(entry.ExtraActions).toBeDefined()
      expect(entry.getExternalUrl).toBeDefined()
      expect(entry.extraSections?.map((s) => s.key)).toEqual([
        'organization-configuration',
        'people',
        'sync-history',
      ])
    })

    it('keeps Workday its extra actions and sync history', () => {
      // Arrange / Act
      const entry = getDetailEntry(connection('Workday'))!

      // Assert
      expect(entry.ExtraActions).toBeDefined()
      expect(entry.extraSections?.map((s) => s.key)).toEqual(['sync-history'])
    })

    it('keeps Entra its sync history', () => {
      // Arrange / Act
      const entry = getDetailEntry(connection('Entra'))!

      // Assert
      expect(entry.extraSections?.map((s) => s.key)).toEqual(['sync-history'])
    })

    it('leaves Azure OpenAI with config alone', () => {
      // Arrange / Act — an AI provider is not sync-shaped, so it has no sync
      // history and no extra sections at all. The page renders no rail.
      const entry = getDetailEntry(connection('Azure OpenAI'))!

      // Assert
      expect(entry.extraSections ?? []).toHaveLength(0)
      expect(entry.ExtraActions).toBeUndefined()
    })
  })
})

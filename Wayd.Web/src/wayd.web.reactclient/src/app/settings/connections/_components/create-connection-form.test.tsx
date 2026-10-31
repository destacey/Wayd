import { ConnectorType } from '@/src/types/connectors'
import {
  buildCreateRequest,
  CreateConnectionFormValues,
  getDiscriminator,
} from './create-connection-form'

describe('create-connection-form helpers', () => {
  describe('getDiscriminator', () => {
    it('should return "azure-devops" for AzureDevOps connector', () => {
      expect(getDiscriminator(ConnectorType.AzureDevOps)).toBe('azure-devops')
    })

    it('should return "azure-openai" for AzureOpenAI connector', () => {
      expect(getDiscriminator(ConnectorType.AzureOpenAI)).toBe('azure-openai')
    })

    it('should return "entra" for Entra connector', () => {
      expect(getDiscriminator(ConnectorType.Entra)).toBe('entra')
    })

    it('should return "workday" for Workday connector', () => {
      expect(getDiscriminator(ConnectorType.Workday)).toBe('workday')
    })

    it('should handle all ConnectorType enum values', () => {
      // Verify we have a discriminator for every enum value
      const connectorTypes = Object.values(ConnectorType).filter(
        (v) => typeof v === 'number',
      ) as ConnectorType[]

      connectorTypes.forEach((type) => {
        const discriminator = getDiscriminator(type)
        expect(discriminator).toBeTruthy()
        expect(typeof discriminator).toBe('string')
      })
    })

    it('should return valid discriminator strings matching backend expectations', () => {
      // These discriminators must match the JsonDerivedType attributes in the backend
      const validDiscriminators = [
        'azure-devops',
        'azure-openai',
        'entra',
        'workday',
      ]

      Object.values(ConnectorType)
        .filter((v) => typeof v === 'number')
        .forEach((type) => {
          const discriminator = getDiscriminator(type as ConnectorType)
          expect(validDiscriminators).toContain(discriminator)
        })
    })

    it('should throw for a connector type with no discriminator', () => {
      // A silent fallback would submit a request the backend cannot deserialize
      expect(() => getDiscriminator(999 as ConnectorType)).toThrow(
        /No request discriminator is registered/,
      )
    })
  })

  describe('buildCreateRequest', () => {
    const azdoValues: CreateConnectionFormValues = {
      name: 'Acme Boards',
      organization: 'acme-org',
      personalAccessToken: 'acme-token',
    }

    it('builds a request for Azure DevOps, whose connector type is 0', () => {
      // Arrange
      const connector = ConnectorType.AzureDevOps

      // Act
      const request = buildCreateRequest(connector, azdoValues)

      // Assert
      expect(request).not.toBeNull()
      expect(request?.$type).toBe('azure-devops')
      expect(request?.organization).toBe('acme-org')
    })

    it('returns null when no connector is chosen', () => {
      // Arrange
      const connector = null

      // Act
      const request = buildCreateRequest(connector, azdoValues)

      // Assert
      expect(request).toBeNull()
    })

    it('puts the discriminator first', () => {
      // Arrange
      const connector = ConnectorType.Entra

      // Act
      const request = buildCreateRequest(connector, {
        name: 'Acme Entra',
        tenantId: 'acme-tenant',
      })

      // Assert
      expect(Object.keys(request!)[0]).toBe('$type')
    })

    it('drops partially filled org exclusions', () => {
      // Arrange
      const values: CreateConnectionFormValues = {
        name: 'Acme Workday',
        orgExclusions: [
          { organizationTypeId: 'cost-center', organizationReference: 'CC-1' },
          { organizationTypeId: 'cost-center', organizationReference: ' ' },
        ],
      }

      // Act
      const request = buildCreateRequest(ConnectorType.Workday, values)

      // Assert
      expect(request?.orgExclusions).toEqual([
        { organizationTypeId: 'cost-center', organizationReference: 'CC-1' },
      ])
    })
  })
})

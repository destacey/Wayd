import { ConnectorType } from '@/src/types/connectors'
import { getDiscriminator } from './create-connection-form'

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
      const validDiscriminators = ['azure-devops', 'azure-openai', 'entra', 'workday']

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
})

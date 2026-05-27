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

    it('should return "openai" for OpenAI connector', () => {
      expect(getDiscriminator(ConnectorType.OpenAI)).toBe('openai')
    })

    it('should return "entra" for Entra connector', () => {
      expect(getDiscriminator(ConnectorType.Entra)).toBe('entra')
    })

    it('should handle all ConnectorType enum values', () => {
      // Verify we have tests for all enum values
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
      const validDiscriminators = ['azure-devops', 'azure-openai', 'openai', 'entra']

      Object.values(ConnectorType)
        .filter((v) => typeof v === 'number')
        .forEach((type) => {
          const discriminator = getDiscriminator(type as ConnectorType)
          expect(validDiscriminators).toContain(discriminator)
        })
    })
  })
})

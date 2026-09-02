'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { MarkReleaseReleasedRequest, ReleaseDto } from '@/src/services/wayd-api'
import { useMarkReleaseReleasedMutation } from '@/src/store/features/product-management/releases-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Alert, DatePicker, Flex, Form, Modal, Typography } from 'antd'
import { Dayjs } from 'dayjs'
import { outstandingContents } from './release-actions'

const { Item } = Form
const { Text } = Typography

export interface MarkReleaseReleasedFormProps {
  release: ReleaseDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface MarkReleaseReleasedFormValues {
  releasedDate: Dayjs
}

/**
 * Records the date a release was announced to customers.
 *
 * Announcing is refused while anything the release carries has not shipped — telling customers
 * 2026.07 is out while a version inside it has not gone anywhere is the one claim a release can make
 * that its own contents contradict. Each contents entry carries its own released date, so the form
 * names which ones are outstanding instead of relaying the API's generic sentence after a failed
 * submit.
 *
 * An empty release announces normally. Emptiness is legitimate — a repackaging or a pricing change is
 * announced with nothing deployed — so only unshipped contents block.
 */
const MarkReleaseReleasedForm = ({
  release,
  onFormComplete,
  onFormCancel,
}: MarkReleaseReleasedFormProps) => {
  const messageApi = useMessage()

  const [markReleased] = useMarkReleaseReleasedMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<MarkReleaseReleasedFormValues>({
      onSubmit: async (values: MarkReleaseReleasedFormValues, form) => {
        try {
          const request = {
            releasedDate: values.releasedDate.format('YYYY-MM-DD'),
          } as unknown as MarkReleaseReleasedRequest

          const response = await markReleased({
            id: release.id,
            cacheKey: release.key,
            request,
          })
          if (response.error) throw response.error

          messageApi.success('Release announced.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while announcing the release. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while announcing the release. Please try again.',
      permission: 'Permissions.Releases.Update',
    })

  const outstanding = outstandingContents(release)
  const isBlocked = outstanding.total > 0

  return (
    <Modal
      title="Mark Released"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid || isBlocked }}
      okText="Announce"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Flex vertical gap={16}>
        {isBlocked && (
          <Alert
            type="warning"
            showIcon
            title={`${release.version} carries something that has not shipped`}
            description={
              <Flex vertical gap={6}>
                <Text type="secondary">
                  Release each of these first, or remove them from this release.
                </Text>
                <Flex vertical gap={2}>
                  {outstanding.packages.map((entry) => (
                    <Text key={entry.id}>
                      {entry.label} <Text type="secondary">(package)</Text>
                    </Text>
                  ))}
                  {outstanding.versions.map((entry) => (
                    <Text key={entry.id}>
                      {entry.label}{' '}
                      <Text type="secondary">(carried directly)</Text>
                    </Text>
                  ))}
                </Flex>
              </Flex>
            }
          />
        )}
        <Form
          form={form}
          size="small"
          layout="vertical"
          name="mark-release-released-form"
        >
          <Item
            label="Announced Date"
            name="releasedDate"
            rules={[{ required: true, message: 'Announced date is required' }]}
            // Shipping and announcing are separate acts, so this is commonly later than the date the
            // contents went out and is deliberately not defaulted from them.
            extra="When customers were told. Often later than the date the contents shipped."
          >
            <DatePicker style={{ width: '100%' }} disabled={isBlocked} />
          </Item>
        </Form>
      </Flex>
    </Modal>
  )
}

export default MarkReleaseReleasedForm

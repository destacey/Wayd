'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import {
  ChangeProjectProgramRequest,
  ProjectDetailsDto,
} from '@/src/services/wayd-api'
import { useGetPortfolioProgramOptionsQuery } from '@/src/store/features/ppm/portfolios-api'
import { useChangeProjectProgramMutation } from '@/src/store/features/ppm/projects-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Flex, Form, Modal, Select, Typography } from 'antd'
import { useEffect } from 'react'

const { Item } = Form
const { Text } = Typography

export interface ChangeProjectProgramFormProps {
  project: ProjectDetailsDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface ChangeProjectProgramFormValues {
  programId?: string
}

const ChangeProjectProgramForm = ({
  project,
  onFormComplete,
  onFormCancel,
}: ChangeProjectProgramFormProps) => {
  const messageApi = useMessage()

  const [changeProjectProgram] = useChangeProjectProgramMutation()

  const {
    data: programData,
    isLoading: programOptionsIsLoading,
    error: programOptionsError,
  } = useGetPortfolioProgramOptionsQuery(project.portfolio.id)

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<ChangeProjectProgramFormValues>({
      onSubmit: async (values: ChangeProjectProgramFormValues, form) => {
          try {
            const request: ChangeProjectProgramRequest = {
              programId: values.programId ?? undefined,
            }
            const response = await changeProjectProgram({
              id: project.id,
              request,
              cacheKey: project.key,
            })
            if (response.error) throw response.error

            messageApi.success('Project program changed successfully.')
            return true
          } catch (error) {
            const apiError: ApiError = isApiError(error) ? error : {}
            if (apiError.status === 422 && apiError.errors) {
              const formErrors = toFormErrors(apiError.errors)
              form.setFields(formErrors)
              messageApi.error('Correct the validation error(s) to continue.')
            } else {
              messageApi.error(
                (isApiError(apiError) ? apiError.detail : undefined) ??
                  'An error occurred while changing the project program. Please try again.',
              )
            }
            return false
          }
        },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while changing the project program. Please try again.',
      permission: 'Permissions.Projects.Update',
    })

  // Initialize form values
  useEffect(() => {
    if (!project) return

    form.setFieldsValue({
      programId: project.program?.id ?? undefined,
    })
  }, [project, form])

  useEffect(() => {
    if (programOptionsError) {
      console.error(programOptionsError)
      messageApi.error(
        (isApiError(programOptionsError) ? programOptionsError.detail : undefined) ??
          'An error occurred while loading program options. Please try again.',
      )
    }
  }, [programOptionsError, messageApi])

  return (
    <Modal
      title="Change Project Program"
      open={isOpen}
      width={'40vw'}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Flex vertical gap="small">
        <Text type="secondary">
          Select a new program to assign this project to, or clear to remove
          the program assignment.
        </Text>
        <Form
          form={form}
          size="small"
          layout="vertical"
          name="change-project-program-form"
        >
          <Item name="programId" label="Program">
            <Select
              allowClear
              options={programData ?? []}
              placeholder="Select Program"
              loading={programOptionsIsLoading}
              optionFilterProp="label"
              filterOption={(input, option) =>
                (option?.label?.toLowerCase() ?? '').includes(
                  input.toLowerCase(),
                )
              }
            />
          </Item>
        </Form>
      </Flex>
    </Modal>
  )
}

export default ChangeProjectProgramForm

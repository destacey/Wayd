'use client'

import { Modal, Spin, Typography } from 'antd'
import { MarkdownRenderer } from '@/src/components/common/markdown'
import { DiscussionMessageDto } from '@/src/services/wayd-api'
import { useGetDiscussionRevisionsQuery } from '@/src/store/features/common/discussions-api'
import { useDiscussionContext } from './discussion-context'
import { toDisplayMarkdown } from './discussion-mentions'
import styles from './discussion.module.css'

const { Text } = Typography

export interface DiscussionRevisionsModalProps {
  message: DiscussionMessageDto
  onClose: () => void
}

/** Every version of a message an edit replaced, newest first. */
const DiscussionRevisionsModal = ({
  message,
  onClose,
}: DiscussionRevisionsModalProps) => {
  const { target } = useDiscussionContext()
  const { data, isLoading } = useGetDiscussionRevisionsQuery({
    ...target,
    messageId: message.id,
  })

  return (
    <Modal open title="Earlier versions" footer={null} onCancel={onClose}>
      {isLoading ? (
        <Spin />
      ) : (
        <ol className={styles.entries}>
          {[...(data ?? [])].reverse().map((revision) => (
            <li
              key={String(revision.replacedOn)}
              className={styles.messageBody}
            >
              <Text type="secondary">
                Replaced {new Date(revision.replacedOn).toLocaleString()}
              </Text>
              {revision.subject && (
                <div className={styles.subject}>{revision.subject}</div>
              )}
              <MarkdownRenderer
                markdown={toDisplayMarkdown(revision.body, message.mentions)}
              />
            </li>
          ))}
        </ol>
      )}
    </Modal>
  )
}

export default DiscussionRevisionsModal

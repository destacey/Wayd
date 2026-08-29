'use client'

import { createContext, FC, ReactNode, useContext } from 'react'
import { message } from 'antd'
import { MessageInstance } from 'antd/es/message/interface'

const MessageContext = createContext<MessageInstance | null>(null)

export const MessageProvider: FC<{ children: ReactNode }> = ({ children }) => {
  const [messageApi, contextHolder] = message.useMessage()

  return (
    <MessageContext.Provider value={messageApi}>
      {contextHolder}
      {children}
    </MessageContext.Provider>
  )
}

export const useMessage = () => {
  const context = useContext(MessageContext)
  if (!context)
    throw new Error('useMessage must be used within a MessageProvider')
  return context
}

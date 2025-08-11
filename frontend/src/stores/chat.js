import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { ElMessage } from 'element-plus'
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { apiService } from '@/services/api'
import { useAuthStore } from '@/stores/auth'

export const useChatStore = defineStore('chat', () => {
  // State
  const connection = ref(null)
  const connectionState = ref('disconnected')
  const friends = ref([])
  const rooms = ref([])
  const messages = ref([])
  const activeChat = ref(null) // { type: 'private|room', data: friend/room }
  const loading = ref(false)

  // Normalizer to handle PascalCase/camelCase payloads from API/SignalR
  const normalizeMessage = (m) => {
    if (!m) return m
    const sender = m.sender || m.Sender || {}
    const receiver = m.receiver || m.Receiver || null
    const fa = m.fileAttachment || m.FileAttachment || null
    return {
      id: m.id ?? m.Id,
      content: m.content ?? m.Content ?? '',
      sentAt: m.sentAt ?? m.SentAt,
      type: m.type ?? m.Type ?? 0,
      roomId: m.roomId ?? m.RoomId ?? null,
      receiverId: m.receiverId ?? m.ReceiverId ?? (receiver ? (receiver.id ?? receiver.Id) : null),
      isRead: m.isRead ?? m.IsRead ?? false,
      readAt: m.readAt ?? m.ReadAt ?? null,
      sender: sender ? {
        id: sender.id ?? sender.Id,
        username: sender.username ?? sender.Username,
        email: sender.email ?? sender.Email,
        createdAt: sender.createdAt ?? sender.CreatedAt,
        isOnline: sender.isOnline ?? sender.IsOnline,
        lastSeen: sender.lastSeen ?? sender.LastSeen,
      } : null,
      receiver: receiver ? {
        id: receiver.id ?? receiver.Id,
        username: receiver.username ?? receiver.Username,
        email: receiver.email ?? receiver.Email,
        createdAt: receiver.createdAt ?? receiver.CreatedAt,
        isOnline: receiver.isOnline ?? receiver.IsOnline,
        lastSeen: receiver.lastSeen ?? receiver.LastSeen,
      } : null,
      fileAttachment: fa ? {
        id: fa.id ?? fa.Id,
        fileName: fa.fileName ?? fa.FileName,
        originalFileName: fa.originalFileName ?? fa.OriginalFileName,
        contentType: fa.contentType ?? fa.ContentType,
        fileSize: fa.fileSize ?? fa.FileSize,
        filePath: fa.filePath ?? fa.FilePath,
        thumbnailPath: fa.thumbnailPath ?? fa.ThumbnailPath,
        uploadedAt: fa.uploadedAt ?? fa.UploadedAt,
        fileType: fa.fileType ?? fa.FileType,
        fileUrl: fa.fileUrl ?? fa.FileUrl,
        thumbnailUrl: fa.thumbnailUrl ?? fa.ThumbnailUrl,
      } : null,
    }
  }

  // Computed
  const isConnected = computed(() => connectionState.value === 'connected')
  const activeChatId = computed(() => {
    if (!activeChat.value) return null
    return `${activeChat.value.type}-${activeChat.value.data.id}`
  })

  // SignalR Connection
  const connectSignalR = async () => {
    try {
      const authStore = useAuthStore()
      if (!authStore.token || !authStore.user) {
        throw new Error('Not authenticated')
      }

      if (connection.value) {
        await disconnectSignalR()
      }

      connection.value = new HubConnectionBuilder()
        .withUrl('http://localhost:5138/chathub', {
          accessTokenFactory: () => authStore.token,
          withCredentials: true
        })
        .withAutomaticReconnect()
        .configureLogging(LogLevel.Information)
        .build()

      // Event handlers
      connection.value.on('ReceiveMessage', (message) => {
        console.log('Received message:', message)
        addMessageToList(message)
        showNotification(message)
      })

      connection.value.on('ReceivePrivateMessage', (message) => {
        console.log('Received private message:', message)
        addMessageToList(message)
        showNotification(message)
      })

      connection.value.on('UserOnline', (userId) => {
        updateUserOnlineStatus(userId, true)
      })

      connection.value.on('UserOffline', (userId) => {
        updateUserOnlineStatus(userId, false)
      })

      connection.value.on('FriendRequestReceived', (senderUsername) => {
        ElMessage.info(`Friend request from ${senderUsername}`)
        // Could refresh friend requests here
      })

      connection.value.on('Error', (error) => {
        console.error('SignalR Error:', error)
        ElMessage.error(`SignalR Error: ${error}`)
      })

      // Connection state handlers
      connection.value.onreconnecting(() => {
        connectionState.value = 'reconnecting'
        console.log('SignalR reconnecting...')
      })

      connection.value.onreconnected(async () => {
        connectionState.value = 'connected'
        console.log('SignalR reconnected')
        // Rejoin chat + all rooms
        await joinChat()
        await joinAllRooms()
      })

      connection.value.onclose(() => {
        connectionState.value = 'disconnected'
        console.log('SignalR disconnected')
      })

      // Start connection
      await connection.value.start()
      connectionState.value = 'connected'
      
      // Join chat with user ID and all rooms
      await joinChat()
      await joinAllRooms()
      
      console.log('SignalR connected successfully')
      return true

    } catch (error) {
      console.error('SignalR connection failed:', error)
      connectionState.value = 'disconnected'
      throw error
    }
  }

  const disconnectSignalR = async () => {
    if (connection.value) {
      try {
        await connection.value.stop()
      } catch (error) {
        console.error('Error stopping SignalR connection:', error)
      }
      connection.value = null
      connectionState.value = 'disconnected'
    }
  }

  const joinChat = async () => {
    if (!connection.value || !isConnected.value) return
    
    try {
      const authStore = useAuthStore()
      await connection.value.invoke('JoinChat', authStore.user.id.toString())
      console.log('Joined chat successfully')
    } catch (error) {
      console.error('Failed to join chat:', error)
    }
  }

  // Join all SignalR room groups for current user
  const joinAllRooms = async () => {
    if (!connection.value || !isConnected.value) return
    try {
      for (const room of rooms.value) {
        await connection.value.invoke('JoinRoom', room.id.toString())
      }
      console.log('Joined all rooms on SignalR')
    } catch (error) {
      console.error('Failed to join all rooms:', error)
    }
  }

  // API Calls
  const loadFriends = async () => {
    loading.value = true
    try {
      const data = await apiService.get('/friend/list')
      friends.value = data
      return data
    } catch (error) {
      console.error('Failed to load friends:', error)
      ElMessage.error('Failed to load friends')
      throw error
    } finally {
      loading.value = false
    }
  }

  const loadRooms = async () => {
    loading.value = true
    try {
      const data = await apiService.get('/room/my')
      rooms.value = data
      // Ensure SignalR joins the room groups
      await joinAllRooms()
      return data
    } catch (error) {
      console.error('Failed to load rooms:', error)
      ElMessage.error('Failed to load rooms')
      throw error
    } finally {
      loading.value = false
    }
  }

  const loadAllRooms = async () => {
    try {
      const data = await apiService.get('/room')
      return data
    } catch (error) {
      console.error('Failed to load all rooms:', error)
      throw error
    }
  }

  const joinRoom = async (roomId) => {
    try {
      const response = await apiService.post(`/room/${roomId}/join`)
      // Also join the SignalR group immediately
      if (connection.value && isConnected.value) {
        try { await connection.value.invoke('JoinRoom', roomId.toString()) } catch (e) { console.warn('JoinRoom invoke failed', e) }
      }
      return response
    } catch (error) {
      console.error('Failed to join room:', error)
      throw error
    }
  }

  const loadPrivateMessages = async (otherUserId) => {
    loading.value = true
    try {
      const data = await apiService.get(`/message/private/${otherUserId}`)
      messages.value = (data || []).map(normalizeMessage)
      return messages.value
    } catch (error) {
      console.error('Failed to load private messages:', error)
      ElMessage.error('Failed to load messages')
      throw error
    } finally {
      loading.value = false
    }
  }

  const loadRoomMessages = async (roomId) => {
    loading.value = true
    try {
      const data = await apiService.get(`/message/room/${roomId}`)
      messages.value = (data || []).map(normalizeMessage)
      return messages.value
    } catch (error) {
      console.error('Failed to load room messages:', error)
      ElMessage.error('Failed to load messages')
      throw error
    } finally {
      loading.value = false
    }
  }

  const sendMessage = async (content, fileAttachmentId = null) => {
    if (!activeChat.value) {
      throw new Error('No active chat')
    }

    const messageData = {
      content: content || '',
      type: fileAttachmentId ? 2 : 0, // File or Text
      fileAttachmentId
    }

    if (activeChat.value.type === 'private') {
      messageData.receiverId = activeChat.value.data.id
    } else {
      messageData.roomId = activeChat.value.data.id
    }

    try {
      console.log('📤 Sending message:', messageData)
      
      // Send via API first
      const response = await apiService.post('/message', messageData)
      console.log('✅ Message sent, response:', response)
      
      // Add to local messages immediately so sender sees it right away
      addMessageToList(normalizeMessage(response))
      
      return response
    } catch (error) {
      console.error('❌ Failed to send message:', error)
      throw error
    }
  }

  const sendFriendRequest = async (usernameOrEmail) => {
    try {
      const response = await apiService.post('/friend/send-request', {
        usernameOrEmail
      })
      return response
    } catch (error) {
      console.error('Failed to send friend request:', error)
      throw error
    }
  }

  const createRoom = async (name, description) => {
    try {
      const response = await apiService.post('/room', {
        name,
        description
      })
      return response
    } catch (error) {
      console.error('Failed to create room:', error)
      throw error
    }
  }

  const uploadAndSendFile = async (file, caption = '') => {
    try {
      // Upload file first
      const uploadResponse = await apiService.uploadFile(file, (progress) => {
        console.log(`Upload progress: ${progress}%`)
      })

      // Send message with file attachment
      await sendMessage(caption, uploadResponse.id)
      
      return uploadResponse
    } catch (error) {
      console.error('Failed to upload and send file:', error)
      throw error
    }
  }

  // Chat Management
  const setActiveChat = (type, data) => {
    activeChat.value = { type, data }
    messages.value = [] // Clear previous messages
    
    // Clear unread count when opening a private chat
    if (type === 'private') {
      const friend = friends.value.find(f => f.friend.id === data.id)
      if (friend) {
        friend.unreadMessageCount = 0
      }
    }
  }

  const clearActiveChat = () => {
    activeChat.value = null
    messages.value = []
  }

  // Message Management
  const addMessageToList = (message) => {
    const normalized = normalizeMessage(message)
    console.log('📨 New message received:', normalized)
    console.log('🎯 Current active chat:', activeChat.value)
    
    // Always update the friends/rooms last message first
    updateLastMessage(normalized)
    
    // If no active chat, just update sidebar and bail
    if (!activeChat.value) {
      console.log('❌ No active chat, only updating sidebar')
      return
    }

    const authStore = useAuthStore()
    const currentUserId = authStore.user.id
    let shouldAddToChat = false

    if (activeChat.value.type === 'private') {
      const friendId = activeChat.value.data.id
      
      const isPrivateMessage = normalized.roomId === null || normalized.roomId === undefined
      const involvesCurrentUser = normalized.sender?.id === currentUserId || normalized.receiverId === currentUserId
      const involvesFriend = normalized.sender?.id === friendId || normalized.receiverId === friendId
      
      shouldAddToChat = isPrivateMessage && involvesCurrentUser && involvesFriend
      
      console.log('💬 Private chat check:', {
        friendId,
        currentUserId,
        messageSender: normalized.sender?.id,
        messageReceiver: normalized.receiverId,
        isPrivateMessage,
        involvesCurrentUser,
        involvesFriend,
        shouldAdd: shouldAddToChat
      })
      
    } else if (activeChat.value.type === 'room') {
      const roomId = activeChat.value.data.id
      const isRoomMessage = normalized.roomId === roomId
      shouldAddToChat = isRoomMessage
      
      console.log('🏠 Room chat check:', {
        roomId,
        messageRoomId: normalized.roomId,
        shouldAdd: shouldAddToChat
      })
    }

    if (shouldAddToChat) {
      // Check if message already exists
      const exists = messages.value.find(m => (m.id ?? m.Id) === normalized.id)
      if (!exists) {
        messages.value.push(normalized)
        messages.value.sort((a, b) => new Date(a.sentAt) - new Date(b.sentAt))
        console.log('✅ Message added to chat! Total messages:', messages.value.length)
      } else {
        console.log('⚠️ Message already exists, skipping')
      }
    } else {
      console.log('❌ Message not for current chat')
    }
  }

  const updateLastMessage = (message) => {
    const msg = normalizeMessage(message)
    const authStore = useAuthStore()
    
    if (msg.roomId) {
      // Update room's last message
      const room = rooms.value.find(r => r.id === msg.roomId)
      if (room) {
        room.lastMessage = {
          content: msg.content || 'File sent',
          sentAt: msg.sentAt,
          senderName: msg.sender?.username
        }
      }
    } else {
      // Update friend's last message
      const otherUserId = msg.sender?.id === authStore.user.id 
        ? msg.receiverId 
        : msg.sender?.id
      
      if (!otherUserId) return
      
      const friend = friends.value.find(f => f.friend.id === otherUserId)
      if (friend) {
        friend.lastMessage = {
          content: msg.content || 'File sent',
          sentAt: msg.sentAt,
          senderName: msg.sender?.username
        }
        
        const isFromCurrentUser = msg.sender?.id === authStore.user.id
        const isViewingThisChat = activeChat.value && 
                                activeChat.value.type === 'private' && 
                                activeChat.value.data.id === otherUserId
        
        if (!isFromCurrentUser && !isViewingThisChat) {
          friend.unreadMessageCount = (friend.unreadMessageCount || 0) + 1
        }
        
        console.log('Updated friend last message:', {
          friendId: friend.friend.id,
          lastMessage: friend.lastMessage,
          unreadCount: friend.unreadMessageCount,
          isFromCurrentUser,
          isViewingThisChat
        })
      }
    }
  }

  const updateUserOnlineStatus = (userId, isOnline) => {
    // Update in friends list
    const friend = friends.value.find(f => f.friend.id === parseInt(userId))
    if (friend) {
      friend.friend.isOnline = isOnline
      if (!isOnline) {
        friend.friend.lastSeen = new Date().toISOString()
      }
    }

    // Update in active chat if it's a private chat
    if (activeChat.value && 
        activeChat.value.type === 'private' && 
        activeChat.value.data.id === parseInt(userId)) {
      activeChat.value.data.isOnline = isOnline
      if (!isOnline) {
        activeChat.value.data.lastSeen = new Date().toISOString()
      }
    }
  }

  const showNotification = (message) => {
    const msg = normalizeMessage(message)
    const authStore = useAuthStore()
    
    // Don't show notification for own messages
    if (msg.sender?.id === authStore.user.id) return

    // Don't show notification if the message is for current active chat and user is viewing it
    if (activeChat.value) {
      const isCurrentChat = 
        (activeChat.value.type === 'private' && 
         (msg.roomId === null || msg.roomId === undefined) &&
         (msg.sender?.id === activeChat.value.data.id || 
          msg.receiverId === activeChat.value.data.id)) ||
        (activeChat.value.type === 'room' && 
         msg.roomId === activeChat.value.data.id)
      
      if (isCurrentChat) return
    }

    // Show notification
    const sender = msg.sender?.username
    const content = msg.content || 'File sent'
    const isRoom = msg.roomId != null

    if (isRoom) {
      const room = rooms.value.find(r => r.id === msg.roomId)
      const roomName = room?.name || 'Room'
      ElMessage.info(`${sender} in ${roomName}: ${content}`)
    } else {
      ElMessage.info(`${sender}: ${content}`)
    }

    // Browser notification (if permission granted)
    if (Notification.permission === 'granted') {
      new Notification(`New message from ${sender}` , {
        body: content,
        icon: '/favicon.ico'
      })
    }
  }

  // Request notification permission
  const requestNotificationPermission = async () => {
    if ('Notification' in window && Notification.permission === 'default') {
      await Notification.requestPermission()
    }
  }

  // Cleanup
  const cleanup = () => {
    disconnectSignalR()
    friends.value = []
    rooms.value = []
    messages.value = []
    activeChat.value = null
  }

  return {
    // State
    connection,
    connectionState,
    friends,
    rooms,
    messages,
    activeChat,
    loading,
    
    // Computed
    isConnected,
    activeChatId,
    
    // SignalR
    connectSignalR,
    disconnectSignalR,
    
    // API Methods
    loadFriends,
    loadRooms,
    loadAllRooms,
    joinRoom,
    loadPrivateMessages,
    loadRoomMessages,
    sendMessage,
    sendFriendRequest,
    createRoom,
    uploadAndSendFile,
    
    // Chat Management
    setActiveChat,
    clearActiveChat,
    
    // Utils
    requestNotificationPermission,
    cleanup
  }
})
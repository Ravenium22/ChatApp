<template>
  <div class="chat-container">
    <!-- Sidebar -->
    <div class="sidebar">
      <!-- User Header -->
      <div class="user-header">
        <div class="user-info">
          <el-avatar :size="40" src="">
            {{ user?.username?.[0]?.toUpperCase() }}
          </el-avatar>
          <div class="user-details">
            <div class="username">{{ user?.username }}</div>
            <div class="status" :class="{ online: true }">
              <span class="status-dot"></span>
              Online
            </div>
          </div>
        </div>
        
        <el-dropdown @command="handleUserMenuCommand">
          <el-button type="text" :icon="MoreFilled" />
          <template #dropdown>
            <el-dropdown-menu>
              <el-dropdown-item command="profile">Profile</el-dropdown-item>
              <el-dropdown-item command="settings">Settings</el-dropdown-item>
              <el-dropdown-item divided command="logout">Logout</el-dropdown-item>
            </el-dropdown-menu>
          </template>
        </el-dropdown>
      </div>

      <!-- Search -->
      <div class="search-section">
        <el-input
          v-model="searchQuery"
          placeholder="Search conversations..."
          prefix-icon="Search"
          size="small"
          clearable
        />
      </div>

      <!-- Navigation Tabs -->
      <el-tabs v-model="activeTab" class="sidebar-tabs">
        <el-tab-pane label="Chats" name="chats">
          <div class="chat-list">
            <!-- Friends List -->
            <div class="section-header">
              <span>Friends</span>
              <el-button 
                type="text" 
                :icon="Plus" 
                size="small"
                @click="showAddFriend = true"
              />
            </div>
            
            <div v-if="friends.length === 0" class="empty-state">
              <p>No friends yet</p>
              <el-button type="text" @click="showAddFriend = true">
                Add your first friend
              </el-button>
            </div>
            
            <div v-else class="friend-list">
              <div 
                v-for="friend in filteredFriends" 
                :key="friend.friend.id"
                class="chat-item"
                :class="{ active: activeChatId === `friend-${friend.friend.id}` }"
                @click="openPrivateChat(friend.friend)"
              >
                <el-avatar :size="40">
                  {{ friend.friend.username[0].toUpperCase() }}
                </el-avatar>
                
                <div class="chat-info">
                  <div class="chat-header">
                    <span class="chat-name">{{ friend.friend.username }}</span>
                    <span class="chat-time" v-if="friend.lastMessage">
                      {{ formatTime(friend.lastMessage.sentAt) }}
                    </span>
                  </div>
                  
                  <div class="chat-preview">
                    <span v-if="friend.lastMessage">
                      {{ friend.lastMessage.content || 'File sent' }}
                    </span>
                    <span v-else class="no-messages">No messages yet</span>
                  </div>
                </div>
                
                <div class="chat-status">
                  <div 
                    class="online-indicator" 
                    :class="{ online: friend.friend.isOnline }"
                  ></div>
                  <el-badge 
                    v-if="friend.unreadMessageCount > 0"
                    :value="friend.unreadMessageCount"
                    class="unread-badge"
                  />
                </div>
              </div>
            </div>
          </div>
        </el-tab-pane>
        
        <el-tab-pane label="Rooms" name="rooms">
          <div class="room-list">
            <div class="section-header">
              <span>Rooms</span>
              <div class="room-actions">
                <el-button 
                  type="text" 
                  :icon="Search" 
                  size="small"
                  @click="showBrowseRooms = true"
                  title="Browse Rooms"
                />
                <el-button 
                  type="text" 
                  :icon="Plus" 
                  size="small"
                  @click="showCreateRoom = true"
                  title="Create Room"
                />
              </div>
            </div>
            
            <div v-if="rooms.length === 0" class="empty-state">
              <p>No rooms joined</p>
              <el-button type="text" @click="showCreateRoom = true">
                Create or join a room
              </el-button>
            </div>
            
            <div v-else class="room-items">
              <div 
                v-for="room in filteredRooms" 
                :key="room.id"
                class="chat-item"
                :class="{ active: activeChatId === `room-${room.id}` }"
                @click="openRoomChat(room)"
              >
                <el-avatar :size="40" :style="{ background: getRandomColor(room.id) }">
                  {{ room.name[0].toUpperCase() }}
                </el-avatar>
                
                <div class="chat-info">
                  <div class="chat-header">
                    <span class="chat-name">{{ room.name }}</span>
                    <span class="chat-time" v-if="room.lastMessage">
                      {{ formatTime(room.lastMessage.sentAt) }}
                    </span>
                  </div>
                  
                  <div class="chat-preview">
                    <span v-if="room.lastMessage">
                      {{ room.lastMessage.senderName }}: {{ room.lastMessage.content }}
                    </span>
                    <span v-else class="no-messages">No messages yet</span>
                  </div>
                </div>
                
                <div class="chat-status">
                  <span class="member-count">{{ room.memberCount }}</span>
                </div>
              </div>
            </div>
          </div>
        </el-tab-pane>
      </el-tabs>
    </div>

    <!-- Main Chat Area -->
    <div class="main-chat">
      <!-- Chat Header -->
      <div v-if="activeChat" class="chat-header">
        <div class="chat-title">
          <el-avatar :size="36" :style="getChatAvatarStyle()">
            {{ getChatInitial() }}
          </el-avatar>
          <div class="title-info">
            <h3>{{ getChatName() }}</h3>
            <p class="subtitle">{{ getChatSubtitle() }}</p>
          </div>
        </div>
        
        <div class="chat-actions">
          <el-button type="text" :icon="Phone" />
          <el-button type="text" :icon="VideoCamera" />
          <el-button type="text" :icon="MoreFilled" />
        </div>
      </div>

      <!-- Welcome State -->
      <div v-if="!activeChat" class="welcome-state">
        <div class="welcome-content">
          <el-icon size="120" color="#e0e0e0">
            <ChatDotRound />
          </el-icon>
          <h2>Welcome to ChatApp</h2>
          <p>Select a friend or room to start messaging</p>
        </div>
      </div>

      <!-- Chat Messages -->
      <div v-else class="messages-container" ref="messagesContainer">
        <div class="messages-list">
          <div 
            v-for="message in messages" 
            :key="message.id"
            class="message-wrapper"
            :class="{ 'own-message': message.sender.id === user?.id }"
          >
            <div class="message-content">
              <div class="message-bubble">
                <div v-if="!isOwnMessage(message)" class="sender-name">
                  {{ message.sender.username }}
                </div>
                
                <!-- Text Message -->
                <div v-if="message.type === 0" class="text-message">
                  {{ message.content }}
                </div>
                
                <!-- File Message -->
                <div v-else-if="message.fileAttachment" class="file-message">
                  <div v-if="isImageFile(message.fileAttachment)" class="image-message">
                    <img 
                      :src="message.fileAttachment.fileUrl" 
                      :alt="message.fileAttachment.originalFileName"
                      @click="previewImage(message.fileAttachment.fileUrl)"
                    />
                    <div v-if="message.content" class="file-caption">
                      {{ message.content }}
                    </div>
                  </div>
                  
                  <div v-else class="document-message">
                    <div class="file-info">
                      <el-icon size="24" color="#409eff">
                        <Document />
                      </el-icon>
                      <div class="file-details">
                        <div class="file-name">{{ message.fileAttachment.originalFileName }}</div>
                        <div class="file-size">{{ formatFileSize(message.fileAttachment.fileSize) }}</div>
                      </div>
                    </div>
                    <el-button 
                      type="primary" 
                      size="small" 
                      @click="downloadFile(message.fileAttachment.fileUrl, message.fileAttachment.originalFileName)"
                    >
                      Download
                    </el-button>
                    <div v-if="message.content" class="file-caption">
                      {{ message.content }}
                    </div>
                  </div>
                </div>
                
                <div class="message-time">
                  {{ formatTime(message.sentAt) }}
                  <el-icon v-if="isOwnMessage(message) && message.isRead" color="#409eff" size="12">
                    <Check />
                  </el-icon>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Message Input -->
      <div v-if="activeChat" class="message-input">
        <div class="input-actions">
          <el-button type="text" :icon="Plus" @click="showAttachMenu = !showAttachMenu" />
          
          <!-- Attachment Menu -->
          <div v-if="showAttachMenu" class="attach-menu">
            <el-button type="text" @click="triggerFileUpload">
              <el-icon><Document /></el-icon>
              <span>File</span>
            </el-button>
            <el-button type="text" @click="triggerImageUpload">
              <el-icon><Picture /></el-icon>
              <span>Image</span>
            </el-button>
          </div>
        </div>
        
        <el-input
          v-model="newMessage"
          placeholder="Type a message..."
          @keydown.enter.exact="sendMessage"
          @keydown.enter.shift.exact.prevent="newMessage += '\n'"
          type="textarea"
          :autosize="{ minRows: 1, maxRows: 4 }"
          resize="none"
        />
        
        <el-button 
          type="primary" 
          :icon="Position"
          @click="sendMessage"
          :disabled="!newMessage.trim() && !uploadingFile"
        />
        
        <!-- Hidden file inputs -->
        <input 
          ref="fileInput" 
          type="file" 
          style="display: none" 
          @change="handleFileUpload"
          accept="*/*"
        />
        <input 
          ref="imageInput" 
          type="file" 
          style="display: none" 
          @change="handleFileUpload"
          accept="image/*"
        />
      </div>
    </div>

    <!-- Modals -->
    <!-- Add Friend Dialog -->
    <el-dialog v-model="showAddFriend" title="Add Friend" width="400px">
      <el-form @submit.prevent="sendFriendRequest">
        <el-form-item label="Username or Email">
          <el-input 
            v-model="newFriendQuery" 
            placeholder="Enter username or email"
            @keyup.enter="sendFriendRequest"
          />
        </el-form-item>
      </el-form>
      
      <template #footer>
        <el-button @click="showAddFriend = false">Cancel</el-button>
        <el-button 
          type="primary" 
          @click="sendFriendRequest"
          :loading="sendingFriendRequest"
        >
          Send Request
        </el-button>
      </template>
    </el-dialog>

    <!-- Create Room Dialog -->
    <el-dialog v-model="showCreateRoom" title="Create Room" width="400px">
      <el-form @submit.prevent="createRoom">
        <el-form-item label="Room Name">
          <el-input 
            v-model="newRoom.name" 
            placeholder="Enter room name"
          />
        </el-form-item>
        <el-form-item label="Description">
          <el-input 
            v-model="newRoom.description" 
            type="textarea"
            placeholder="Enter room description (optional)"
          />
        </el-form-item>
      </el-form>
      
      <template #footer>
        <el-button @click="showCreateRoom = false">Cancel</el-button>
        <el-button 
          type="primary" 
          @click="createRoom"
          :loading="creatingRoom"
        >
          Create Room
        </el-button>
      </template>
    </el-dialog>

    <!-- Browse Rooms Dialog -->
    <el-dialog v-model="showBrowseRooms" title="Browse Rooms" width="500px">
      <div v-loading="loadingRooms" class="browse-rooms">
        <div v-if="availableRooms.length === 0 && !loadingRooms" class="empty-state">
          <p>No rooms available</p>
        </div>
        
        <div v-else class="available-rooms">
          <div 
            v-for="room in availableRooms" 
            :key="room.id"
            class="room-item"
          >
            <el-avatar :size="40" :style="{ background: getRandomColor(room.id) }">
              {{ room.name[0].toUpperCase() }}
            </el-avatar>
            
            <div class="room-details">
              <div class="room-name">{{ room.name }}</div>
              <div class="room-info">
                <span class="member-count">{{ room.memberCount }} members</span>
                <span v-if="room.description" class="room-description">
                  • {{ room.description }}
                </span>
              </div>
            </div>
            
            <el-button 
              type="primary" 
              size="small"
              @click="joinRoom(room.id)"
            >
              Join
            </el-button>
          </div>
        </div>
      </div>
      
      <template #footer>
        <el-button @click="showBrowseRooms = false">Close</el-button>
        <el-button type="primary" @click="loadAvailableRooms">
          <i class="el-icon-refresh"></i> Refresh
        </el-button>
      </template>
    </el-dialog>

    <!-- Image Preview Dialog -->
    <el-dialog v-model="showImagePreview" width="80%" align-center>
      <img :src="previewImageUrl" style="width: 100%; height: auto;" />
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, reactive, computed, onMounted, onUnmounted, nextTick, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { useChatStore } from '@/stores/chat'
import { ElMessage } from 'element-plus'
import { 
  MoreFilled, Plus, Search, Phone, VideoCamera, ChatDotRound, 
  Document, Picture, Position, Check 
} from '@element-plus/icons-vue'

const router = useRouter()
const authStore = useAuthStore()
const chatStore = useChatStore()

// Refs
const messagesContainer = ref()
const fileInput = ref()
const imageInput = ref()

// Reactive data
const searchQuery = ref('')
const activeTab = ref('chats')
const newMessage = ref('')
const showAttachMenu = ref(false)
const uploadingFile = ref(false)

// Modals
const showAddFriend = ref(false)
const showCreateRoom = ref(false)
const showBrowseRooms = ref(false)
const showImagePreview = ref(false)
const previewImageUrl = ref('')

// Available rooms
const availableRooms = ref([])
const loadingRooms = ref(false)

// Friend request
const newFriendQuery = ref('')
const sendingFriendRequest = ref(false)

// Room creation
const newRoom = reactive({
  name: '',
  description: ''
})
const creatingRoom = ref(false)

// Computed
const user = computed(() => authStore.user)
const friends = computed(() => chatStore.friends)
const rooms = computed(() => chatStore.rooms)
const messages = computed(() => chatStore.messages)
const activeChat = computed(() => chatStore.activeChat)
const activeChatId = computed(() => chatStore.activeChatId)

const filteredFriends = computed(() => {
  if (!searchQuery.value) return friends.value
  return friends.value.filter(friend => 
    friend.friend.username.toLowerCase().includes(searchQuery.value.toLowerCase())
  )
})

const filteredRooms = computed(() => {
  if (!searchQuery.value) return rooms.value
  return rooms.value.filter(room => 
    room.name.toLowerCase().includes(searchQuery.value.toLowerCase())
  )
})

// Methods
const handleUserMenuCommand = async (command) => {
  switch (command) {
    case 'logout':
      await authStore.logout()
      router.push('/login')
      break
    case 'profile':
      ElMessage.info('Profile page coming soon!')
      break
    case 'settings':
      ElMessage.info('Settings page coming soon!')
      break
  }
}

const openPrivateChat = (friend) => {
  chatStore.setActiveChat('private', friend)
  chatStore.loadPrivateMessages(friend.id)
}

const openRoomChat = (room) => {
  chatStore.setActiveChat('room', room)
  chatStore.loadRoomMessages(room.id)
}

const sendMessage = async () => {
  if (!newMessage.value.trim() && !uploadingFile.value) return
  
  try {
    await chatStore.sendMessage(newMessage.value.trim())
    newMessage.value = ''
    showAttachMenu.value = false
    scrollToBottom()
  } catch (error) {
    ElMessage.error('Failed to send message')
  }
}

const sendFriendRequest = async () => {
  if (!newFriendQuery.value.trim()) return
  
  sendingFriendRequest.value = true
  try {
    await chatStore.sendFriendRequest(newFriendQuery.value.trim())
    showAddFriend.value = false
    newFriendQuery.value = ''
    ElMessage.success('Friend request sent!')
    chatStore.loadFriends() // Refresh friends list
  } catch (error) {
    // Handle specific error for already friends
    if (error.message?.includes('zaten arkadaş') || error.message?.includes('already')) {
      ElMessage.warning('You are already friends with this person!')
    } else if (error.message?.includes('bulunamadı') || error.message?.includes('not found')) {
      ElMessage.error('User not found. Check the username or email.')
    } else {
      ElMessage.error(error.message || 'Failed to send friend request')
    }
  } finally {
    sendingFriendRequest.value = false
  }
}

const createRoom = async () => {
  if (!newRoom.name.trim()) return
  
  creatingRoom.value = true
  try {
    await chatStore.createRoom(newRoom.name, newRoom.description)
    showCreateRoom.value = false
    newRoom.name = ''
    newRoom.description = ''
    ElMessage.success('Room created successfully!')
    chatStore.loadRooms() // Refresh rooms list
  } catch (error) {
    ElMessage.error(error.message || 'Failed to create room')
  } finally {
    creatingRoom.value = false
  }
}

const triggerFileUpload = () => {
  fileInput.value.click()
  showAttachMenu.value = false
}

const triggerImageUpload = () => {
  imageInput.value.click()
  showAttachMenu.value = false
}

const handleFileUpload = async (event) => {
  const file = event.target.files[0]
  if (!file) return
  
  uploadingFile.value = true
  try {
    await chatStore.uploadAndSendFile(file, newMessage.value.trim())
    newMessage.value = ''
    scrollToBottom()
  } catch (error) {
    ElMessage.error('Failed to upload file')
  } finally {
    uploadingFile.value = false
    // Clear the input
    event.target.value = ''
  }
}

const isOwnMessage = (message) => {
  return message.sender.id === user.value?.id
}

const isImageFile = (fileAttachment) => {
  return fileAttachment.fileType === 1 // FileType.Image
}

const formatTime = (dateString) => {
  return new Date(dateString).toLocaleTimeString('en-US', {
    hour: '2-digit',
    minute: '2-digit'
  })
}

const formatFileSize = (bytes) => {
  if (bytes === 0) return '0 Bytes'
  const k = 1024
  const sizes = ['Bytes', 'KB', 'MB', 'GB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i]
}

const previewImage = (url) => {
  previewImageUrl.value = url
  showImagePreview.value = true
}

const downloadFile = (url, filename) => {
  const link = document.createElement('a')
  link.href = url
  link.download = filename
  link.click()
}

const getChatName = () => {
  if (!activeChat.value) return ''
  return activeChat.value.type === 'private' 
    ? activeChat.value.data.username 
    : activeChat.value.data.name
}

const getChatSubtitle = () => {
  if (!activeChat.value) return ''
  if (activeChat.value.type === 'private') {
    return activeChat.value.data.isOnline ? 'Online' : 'Last seen recently'
  } else {
    return `${activeChat.value.data.memberCount || 0} members`
  }
}

const getChatInitial = () => {
  if (!activeChat.value) return ''
  const name = getChatName()
  return name[0]?.toUpperCase() || ''
}

const getChatAvatarStyle = () => {
  if (!activeChat.value) return {}
  if (activeChat.value.type === 'room') {
    return { background: getRandomColor(activeChat.value.data.id) }
  }
  return {}
}

const loadAvailableRooms = async () => {
  loadingRooms.value = true
  try {
    const response = await chatStore.loadAllRooms()
    availableRooms.value = response
  } catch (error) {
    ElMessage.error('Failed to load available rooms')
  } finally {
    loadingRooms.value = false
  }
}

const joinRoom = async (roomId) => {
  try {
    await chatStore.joinRoom(roomId)
    ElMessage.success('Joined room successfully!')
    showBrowseRooms.value = false
    chatStore.loadRooms() // Refresh my rooms
  } catch (error) {
    if (error.message?.includes('zaten') || error.message?.includes('already')) {
      ElMessage.warning('You are already a member of this room!')
    } else {
      ElMessage.error(error.message || 'Failed to join room')
    }
  }
}

const getRandomColor = (seed) => {
  const colors = ['#409eff', '#67c23a', '#e6a23c', '#f56c6c', '#909399']
  return colors[seed % colors.length]
}

const scrollToBottom = () => {
  nextTick(() => {
    if (messagesContainer.value) {
      messagesContainer.value.scrollTop = messagesContainer.value.scrollHeight
    }
  })
}

// Watch for new messages to auto-scroll
watch(messages, () => {
  scrollToBottom()
}, { deep: true })

// Initialize
onMounted(async () => {
  // Connect to SignalR
  await chatStore.connectSignalR()
  
  // Load initial data
  await Promise.all([
    chatStore.loadFriends(),
    chatStore.loadRooms()
  ])
  
  // Load available rooms for browsing
  loadAvailableRooms()
})

onUnmounted(() => {
  chatStore.disconnectSignalR()
})
</script>

<style scoped>
.chat-container {
  display: flex;
  height: 100vh;
  background: #f5f7fa;
}

.sidebar {
  width: 320px;
  background: white;
  border-right: 1px solid #e4e7ed;
  display: flex;
  flex-direction: column;
}

.user-header {
  padding: 20px;
  border-bottom: 1px solid #e4e7ed;
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.user-info {
  display: flex;
  align-items: center;
  gap: 12px;
}

.user-details {
  flex: 1;
}

.username {
  font-weight: 600;
  color: #303133;
}

.status {
  font-size: 12px;
  color: #909399;
  display: flex;
  align-items: center;
  gap: 4px;
}

.status-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: #67c23a;
}

.search-section {
  padding: 16px 20px;
  border-bottom: 1px solid #e4e7ed;
}

.sidebar-tabs {
  flex: 1;
  display: flex;
  flex-direction: column;
}

.sidebar-tabs :deep(.el-tabs__content) {
  flex: 1;
  overflow: hidden;
}

.sidebar-tabs :deep(.el-tab-pane) {
  height: 100%;
  overflow-y: auto;
}

.section-header {
  padding: 16px 20px 8px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  font-weight: 600;
  color: #303133;
  font-size: 14px;
}

.room-actions {
  display: flex;
  gap: 4px;
}

.empty-state {
  padding: 40px 20px;
  text-align: center;
  color: #909399;
}

.chat-item {
  padding: 12px 20px;
  display: flex;
  align-items: center;
  gap: 12px;
  cursor: pointer;
  border-bottom: 1px solid #f0f0f0;
  transition: background-color 0.2s;
}

.chat-item:hover {
  background: #f5f7fa;
}

.chat-item.active {
  background: #ecf5ff;
  border-right: 3px solid #409eff;
}

.chat-info {
  flex: 1;
  min-width: 0;
}

.chat-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 4px;
}

.chat-name {
  font-weight: 500;
  color: #303133;
}

.chat-time {
  font-size: 12px;
  color: #909399;
}

.chat-preview {
  font-size: 13px;
  color: #909399;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.no-messages {
  font-style: italic;
}

.chat-status {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
}

.online-indicator {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: #dcdfe6;
}

.online-indicator.online {
  background: #67c23a;
}

.member-count {
  font-size: 12px;
  color: #909399;
}

.main-chat {
  flex: 1;
  display: flex;
  flex-direction: column;
  background: white;
}

.chat-header {
  padding: 20px;
  border-bottom: 1px solid #e4e7ed;
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.chat-title {
  display: flex;
  align-items: center;
  gap: 12px;
}

.title-info h3 {
  margin: 0;
  color: #303133;
}

.title-info .subtitle {
  margin: 0;
  font-size: 12px;
  color: #909399;
}

.chat-actions {
  display: flex;
  gap: 8px;
}

.welcome-state {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
}

.welcome-content {
  text-align: center;
  color: #909399;
}

.welcome-content h2 {
  margin: 20px 0 8px;
  color: #303133;
}

.messages-container {
  flex: 1;
  overflow-y: auto;
  padding: 20px;
}

.message-wrapper {
  margin-bottom: 16px;
  display: flex;
}

.message-wrapper.own-message {
  justify-content: flex-end;
}

.message-content {
  max-width: 70%;
}

.message-bubble {
  background: #f0f0f0;
  padding: 12px 16px;
  border-radius: 18px;
  position: relative;
}

.own-message .message-bubble {
  background: #409eff;
  color: white;
}

.sender-name {
  font-size: 12px;
  font-weight: 500;
  margin-bottom: 4px;
  color: #409eff;
}

.text-message {
  word-wrap: break-word;
  white-space: pre-wrap;
}

.file-message {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.image-message img {
  max-width: 200px;
  max-height: 200px;
  border-radius: 8px;
  cursor: pointer;
}

.document-message {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px;
  background: rgba(255, 255, 255, 0.1);
  border-radius: 8px;
}

.file-info {
  display: flex;
  align-items: center;
  gap: 8px;
  flex: 1;
}

.file-details {
  flex: 1;
}

.file-name {
  font-weight: 500;
  font-size: 14px;
}

.file-size {
  font-size: 12px;
  opacity: 0.8;
}

.file-caption {
  font-size: 14px;
  margin-top: 8px;
}

.message-time {
  font-size: 11px;
  opacity: 0.7;
  margin-top: 4px;
  display: flex;
  align-items: center;
  gap: 4px;
  justify-content: flex-end;
}

.message-input {
  padding: 20px;
  border-top: 1px solid #e4e7ed;
  display: flex;
  align-items: flex-end;
  gap: 12px;
  position: relative;
}

.input-actions {
  position: relative;
}

.attach-menu {
  position: absolute;
  bottom: 100%;
  left: 0;
  background: white;
  border: 1px solid #e4e7ed;
  border-radius: 8px;
  padding: 8px;
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.1);
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.attach-menu .el-button {
  justify-content: flex-start;
  gap: 8px;
}

:deep(.el-input__inner) {
  border-radius: 20px;
  border: 2px solid #e4e7ed;
}

:deep(.el-input__inner:focus) {
  border-color: #409eff;
}

:deep(.el-textarea__inner) {
  border-radius: 20px;
  border: 2px solid #e4e7ed;
  resize: none;
  padding: 12px 16px;
}

:deep(.el-textarea__inner:focus) {
  border-color: #409eff;
}

.browse-rooms {
  max-height: 400px;
  overflow-y: auto;
}

.available-rooms {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.room-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px;
  border: 1px solid #e4e7ed;
  border-radius: 8px;
  transition: all 0.2s;
}

.room-item:hover {
  background: #f5f7fa;
  border-color: #409eff;
}

.room-details {
  flex: 1;
  min-width: 0;
}

.room-name {
  font-weight: 500;
  color: #303133;
  margin-bottom: 4px;
}

.room-info {
  font-size: 12px;
  color: #909399;
  display: flex;
  align-items: center;
  gap: 8px;
}

.room-description {
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

:deep(.el-button--primary) {
  border-radius: 50%;
  width: 40px;
  height: 40px;
  padding: 0;
}
</style>
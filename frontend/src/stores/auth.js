import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { ElMessage } from 'element-plus'
import { apiService } from '@/services/api'

export const useAuthStore = defineStore('auth', () => {
  const user = ref(null)
  const token = ref(localStorage.getItem('authToken') || '')
  const loading = ref(false)

  const isAuthenticated = computed(() => !!token.value && !!user.value)

  // Login function
  const login = async (credentials) => {
    loading.value = true
    try {
      const response = await apiService.post('/user/login', credentials)
      
      token.value = response.token
      user.value = response.user
      
      // Store token in localStorage
      localStorage.setItem('authToken', response.token)
      
      // Set default auth header
      apiService.setAuthToken(response.token)
      
      ElMessage.success('Welcome back!')
      return response
    } catch (error) {
      ElMessage.error(error.message || 'Login failed')
      throw error
    } finally {
      loading.value = false
    }
  }

  // Register function
  const register = async (userData) => {
    loading.value = true
    try {
      const response = await apiService.post('/user/register', userData)
      
      token.value = response.token
      user.value = response.user
      
      // Store token in localStorage
      localStorage.setItem('authToken', response.token)
      
      // Set default auth header
      apiService.setAuthToken(response.token)
      
      ElMessage.success('Account created successfully!')
      return response
    } catch (error) {
      ElMessage.error(error.message || 'Registration failed')
      throw error
    } finally {
      loading.value = false
    }
  }

  // Logout function
  const logout = async () => {
    try {
      // Call logout endpoint if we have a token
      if (token.value) {
        await apiService.post('/user/logout')
      }
    } catch (error) {
      console.error('Logout API call failed:', error)
    } finally {
      // Clear local state regardless of API call result
      user.value = null
      token.value = ''
      localStorage.removeItem('authToken')
      apiService.setAuthToken('')
      ElMessage.info('Logged out successfully')
    }
  }

  // Try to restore auth from localStorage
  const tryRestoreAuth = async () => {
    const savedToken = localStorage.getItem('authToken')
    if (!savedToken) return

    token.value = savedToken
    apiService.setAuthToken(savedToken)

    try {
      // Verify token by getting profile
      const response = await apiService.get('/user/profile')
      user.value = response
    } catch (error) {
      // Token is invalid, clear it
      console.error('Token validation failed:', error)
      await logout()
    }
  }

  // Get fresh profile data
  const refreshProfile = async () => {
    try {
      const response = await apiService.get('/user/profile')
      user.value = response
      return response
    } catch (error) {
      ElMessage.error('Failed to refresh profile')
      throw error
    }
  }

  return {
    user,
    token,
    loading,
    isAuthenticated,
    login,
    register,
    logout,
    tryRestoreAuth,
    refreshProfile
  }
})
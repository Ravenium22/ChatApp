import axios from 'axios'

class ApiService {
  constructor() {
    this.client = axios.create({
      baseURL: '/api', // uses vite proxy
      timeout: 10000,
      headers: {
        'Content-Type': 'application/json'
      }
    })

    // Request interceptor
    this.client.interceptors.request.use(
      (config) => {
        console.log(`API Request: ${config.method?.toUpperCase()} ${config.url}`)
        return config
      },
      (error) => Promise.reject(error)
    )

    // Response interceptor
    this.client.interceptors.response.use(
      (response) => {
        console.log(`API Response: ${response.status} ${response.config.url}`)
        return response.data
      },
      (error) => {
        console.error('API Error:', error.response?.data || error.message)
        
        // Handle common errors
        if (error.response?.status === 401) {
          // Token expired or invalid
          localStorage.removeItem('authToken')
          window.location.href = '/login'
        }
        
        const errorMessage = error.response?.data?.message || 
                           error.response?.data?.details || 
                           error.message || 
                           'Network error occurred'
        
        return Promise.reject({ 
          message: errorMessage,
          status: error.response?.status,
          data: error.response?.data 
        })
      }
    )
  }

  setAuthToken(token) {
    if (token) {
      this.client.defaults.headers.common['Authorization'] = `Bearer ${token}`
    } else {
      delete this.client.defaults.headers.common['Authorization']
    }
  }

  // HTTP methods
  get(url, config = {}) {
    return this.client.get(url, config)
  }

  post(url, data = {}, config = {}) {
    return this.client.post(url, data, config)
  }

  put(url, data = {}, config = {}) {
    return this.client.put(url, data, config)
  }

  delete(url, config = {}) {
    return this.client.delete(url, config)
  }

  // File upload helper
  uploadFile(file, onProgress = null) {
    const formData = new FormData()
    formData.append('file', file)

    const config = {
      headers: {
        'Content-Type': 'multipart/form-data'
      }
    }

    if (onProgress) {
      config.onUploadProgress = (progressEvent) => {
        const percentCompleted = Math.round(
          (progressEvent.loaded * 100) / progressEvent.total
        )
        onProgress(percentCompleted)
      }
    }

    return this.client.post('/file/upload', formData, config)
  }
}

export const apiService = new ApiService()
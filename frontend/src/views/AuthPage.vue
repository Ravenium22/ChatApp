<template>
  <div class="auth-container">
    <div class="auth-card">
      <div class="auth-header">
        <h1>💬 ChatApp</h1>
        <p>Connect with friends in real-time</p>
      </div>

      <el-tabs v-model="activeTab" class="auth-tabs">
        <!-- Login Tab -->
        <el-tab-pane label="Login" name="login">
          <el-form 
            ref="loginFormRef" 
            :model="loginForm" 
            :rules="loginRules"
            @submit.prevent="handleLogin"
          >
            <el-form-item prop="email">
              <el-input
                v-model="loginForm.email"
                placeholder="Email"
                prefix-icon="Message"
                size="large"
                @keyup.enter="handleLogin"
              />
            </el-form-item>
            
            <el-form-item prop="password">
              <el-input
                v-model="loginForm.password"
                type="password"
                placeholder="Password"
                prefix-icon="Lock"
                size="large"
                show-password
                @keyup.enter="handleLogin"
              />
            </el-form-item>
            
            <el-form-item>
              <el-button 
                type="primary" 
                size="large" 
                :loading="authStore.loading"
                @click="handleLogin"
                style="width: 100%"
              >
                <span v-if="!authStore.loading">Sign In</span>
                <span v-else>Signing In...</span>
              </el-button>
            </el-form-item>
          </el-form>
        </el-tab-pane>

        <!-- Register Tab -->
        <el-tab-pane label="Register" name="register">
          <el-form 
            ref="registerFormRef" 
            :model="registerForm" 
            :rules="registerRules"
            @submit.prevent="handleRegister"
          >
            <el-form-item prop="username">
              <el-input
                v-model="registerForm.username"
                placeholder="Username"
                prefix-icon="User"
                size="large"
              />
            </el-form-item>
            
            <el-form-item prop="email">
              <el-input
                v-model="registerForm.email"
                placeholder="Email"
                prefix-icon="Message"
                size="large"
              />
            </el-form-item>
            
            <el-form-item prop="password">
              <el-input
                v-model="registerForm.password"
                type="password"
                placeholder="Password"
                prefix-icon="Lock"
                size="large"
                show-password
              />
            </el-form-item>
            
            <el-form-item prop="confirmPassword">
              <el-input
                v-model="registerForm.confirmPassword"
                type="password"
                placeholder="Confirm Password"
                prefix-icon="Lock"
                size="large"
                show-password
                @keyup.enter="handleRegister"
              />
            </el-form-item>
            
            <el-form-item>
              <el-button 
                type="primary" 
                size="large" 
                :loading="authStore.loading"
                @click="handleRegister"
                style="width: 100%"
              >
                <span v-if="!authStore.loading">Create Account</span>
                <span v-else>Creating Account...</span>
              </el-button>
            </el-form-item>
          </el-form>
        </el-tab-pane>
      </el-tabs>

      <!-- Quick Test Users -->
      <div class="quick-login">
        <el-divider>Quick Test</el-divider>
        <div class="test-users">
          <el-button 
            size="small" 
            @click="quickLogin('test1@test.com', '123456')"
            :disabled="authStore.loading"
          >
            Login as User 1
          </el-button>
          <el-button 
            size="small" 
            @click="quickLogin('test2@test.com', '123456')"
            :disabled="authStore.loading"
          >
            Login as User 2
          </el-button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const router = useRouter()
const authStore = useAuthStore()

const activeTab = ref('login')
const loginFormRef = ref()
const registerFormRef = ref()

// Login form
const loginForm = reactive({
  email: 'test1@test.com',
  password: '123456'
})

const loginRules = {
  email: [
    { required: true, message: 'Please input email', trigger: 'blur' },
    { type: 'email', message: 'Please input valid email', trigger: 'blur' }
  ],
  password: [
    { required: true, message: 'Please input password', trigger: 'blur' },
    { min: 6, message: 'Password must be at least 6 characters', trigger: 'blur' }
  ]
}

// Register form
const registerForm = reactive({
  username: '',
  email: '',
  password: '',
  confirmPassword: ''
})

const registerRules = {
  username: [
    { required: true, message: 'Please input username', trigger: 'blur' },
    { min: 3, max: 50, message: 'Username must be 3-50 characters', trigger: 'blur' }
  ],
  email: [
    { required: true, message: 'Please input email', trigger: 'blur' },
    { type: 'email', message: 'Please input valid email', trigger: 'blur' }
  ],
  password: [
    { required: true, message: 'Please input password', trigger: 'blur' },
    { min: 6, message: 'Password must be at least 6 characters', trigger: 'blur' }
  ],
  confirmPassword: [
    { required: true, message: 'Please confirm password', trigger: 'blur' },
    {
      validator: (rule, value, callback) => {
        if (value !== registerForm.password) {
          callback(new Error('Passwords do not match'))
        } else {
          callback()
        }
      },
      trigger: 'blur'
    }
  ]
}

const handleLogin = async () => {
  try {
    await loginFormRef.value.validate()
    await authStore.login(loginForm)
    router.push('/chat')
  } catch (error) {
    console.error('Login failed:', error)
  }
}

const handleRegister = async () => {
  try {
    await registerFormRef.value.validate()
    await authStore.register({
      username: registerForm.username,
      email: registerForm.email,
      password: registerForm.password
    })
    router.push('/chat')
  } catch (error) {
    console.error('Registration failed:', error)
  }
}

const quickLogin = async (email, password) => {
  loginForm.email = email
  loginForm.password = password
  await handleLogin()
}
</script>

<style scoped>
.auth-container {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  padding: 20px;
}

.auth-card {
  width: 100%;
  max-width: 400px;
  background: white;
  border-radius: 16px;
  padding: 40px;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.1);
}

.auth-header {
  text-align: center;
  margin-bottom: 30px;
}

.auth-header h1 {
  font-size: 2.5rem;
  font-weight: bold;
  color: #333;
  margin-bottom: 8px;
}

.auth-header p {
  color: #666;
  font-size: 1rem;
}

.auth-tabs {
  margin-bottom: 20px;
}

.auth-tabs :deep(.el-tabs__header) {
  margin: 0 0 30px 0;
}

.auth-tabs :deep(.el-tabs__nav-wrap::after) {
  display: none;
}

.auth-tabs :deep(.el-tabs__active-bar) {
  background-color: #667eea;
}

.auth-tabs :deep(.el-tabs__item.is-active) {
  color: #667eea;
}

.el-form-item {
  margin-bottom: 24px;
}

.quick-login {
  margin-top: 20px;
}

.test-users {
  display: flex;
  gap: 10px;
  justify-content: center;
}

.test-users .el-button {
  flex: 1;
}

:deep(.el-input__inner) {
  border-radius: 12px;
  border: 2px solid #f0f0f0;
  transition: all 0.3s;
}

:deep(.el-input__inner:focus) {
  border-color: #667eea;
  box-shadow: 0 0 0 2px rgba(102, 126, 234, 0.1);
}

:deep(.el-button--primary) {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  border: none;
  border-radius: 12px;
  padding: 12px 24px;
  font-weight: 600;
  transition: all 0.3s;
}

:deep(.el-button--primary:hover) {
  transform: translateY(-2px);
  box-shadow: 0 10px 25px rgba(102, 126, 234, 0.3);
}
</style>
<script setup lang="ts">
import { ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import type { VForm } from 'vuetify/components'
import { useAuthStore } from '@/stores/auth'
import toolboxLogo from '@/assets/toolbox-logo.svg'

const auth = useAuthStore()
const route = useRoute()
const router = useRouter()

// STATE
const form = ref<VForm | null>(null)
const email = ref('')
const password = ref('')
const showPassword = ref(false)
const loginFailed = ref(false)

// CONSTANTS
const rules = {
  required: (v: string) => !!v || 'Υποχρεωτικό πεδίο',
  email: (v: string) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v) || 'Μη έγκυρο email',
}

// METHODS
function getRedirect() {
  const redirect = route.query.redirect
  // Only allow in-app paths, never another site (e.g. "//evil.com")
  if (typeof redirect === 'string' && redirect.startsWith('/') && !redirect.startsWith('//')) {
    return redirect
  }
  return { name: 'home' }
}

async function submit() {
  const result = await form.value?.validate()
  if (!result?.valid) return

  loginFailed.value = false
  const ok = await auth.login(email.value, password.value)

  if (!ok) {
    loginFailed.value = true
    password.value = ''
    return
  }

  // replace, so the back button doesn't return to the login page
  router.replace(getRedirect())
}
</script>

<template>
  <v-main>
    <div class="login-page">
      <v-card class="login-card" rounded="xl" elevation="0" border>
        <div class="header">
          <img :src="toolboxLogo" alt="Toolbox" class="logo" />
          <h1 class="title">Mail Archive</h1>
          <p class="text-medium-emphasis text-body-2">Συνδεθείτε με τον εταιρικό σας λογαριασμό</p>
        </div>

        <v-alert
          v-if="loginFailed"
          type="error"
          variant="tonal"
          density="compact"
          class="mb-4"
          text="Λάθος email ή κωδικός πρόσβασης"
          data-testid="login-alert-message"
        />

        <v-form ref="form" @submit.prevent="submit">
          <v-text-field
            v-model="email"
            label="Email"
            type="email"
            autocomplete="username"
            prepend-inner-icon="i-mdi-email-outline"
            variant="outlined"
            :rules="[rules.required, rules.email]"
            :disabled="auth.isLoginLoading"
            autofocus
            class="mb-2"
            clearable
            data-testid="email-input"
          />

          <v-text-field
            v-model="password"
            label="Κωδικός πρόσβασης"
            :type="showPassword ? 'text' : 'password'"
            autocomplete="current-password"
            prepend-inner-icon="i-mdi-lock-outline"
            variant="outlined"
            :rules="[rules.required]"
            :disabled="auth.isLoginLoading"
            @click:append-inner="showPassword = !showPassword"
            clearable
            data-testid="password-input"
          />

          <v-btn
            type="submit"
            color="primary"
            size="large"
            rounded="pill"
            block
            flat
            class="mt-4"
            :loading="auth.isLoginLoading"
            data-testid="login-button"
          >
            Σύνδεση
          </v-btn>
        </v-form>

        <!-- <p class="footer text-caption text-medium-emphasis">
          Μόνο για εργαζόμενους. Για πρόσβαση ή επαναφορά κωδικού, επικοινωνήστε με το τμήμα IT.
        </p> -->
      </v-card>
    </div>
  </v-main>
</template>

<style scoped>
.login-page {
  min-height: 100dvh;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 16px;
  background: rgb(var(--v-theme-background));
}

.login-card {
  width: 100%;
  max-width: 400px;
  padding: 32px;
}

.header {
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
}

.logo {
  width: 195px;
  height: 195px;
  object-fit: contain;
}

.title {
  font-size: 1.5rem;
  font-weight: 500;
  margin: 8px 0 4px;
}

.footer {
  text-align: center;
  margin: 24px 0 0;
}

@media (max-width: 600px) {
  .login-card {
    padding: 24px 20px;
  }
}
</style>

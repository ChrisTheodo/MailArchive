<script setup lang="ts">
import { useRouter, type RouteLocationRaw } from 'vue-router'

const props = withDefaults(
  defineProps<{
    code?: string
    title?: string
    message?: string
    icon?: string
    showBack?: boolean
    backTo?: RouteLocationRaw
  }>(),
  {
    code: undefined,
    title: 'Η σελίδα δεν βρέθηκε',
    message: 'Η σελίδα που ψάχνετε δεν υπάρχει ή έχει μετακινηθεί',
    icon: 'i-mdi-map-marker-question-outline',
    showBack: true,
    backTo: undefined,
  },
)

const router = useRouter()

// METHODS
function goBack() {
  if (props.backTo) {
    router.push(props.backTo)
  } else if (window.history.state?.back) {
    router.back()
  } else {
    router.push('/')
  }
}
</script>

<template>
  <v-empty-state :headline="code" :title="title" :text="message" :icon="icon">
    <template #actions>
      <slot name="actions">
        <v-btn
          v-if="showBack"
          prepend-icon="i-mdi-arrow-left"
          variant="outlined"
          rounded="pill"
          @click="goBack"
          data-testid="not-found-go-back-button"
        >
          Επιστροφή
        </v-btn>
      </slot>
    </template>
  </v-empty-state>
</template>

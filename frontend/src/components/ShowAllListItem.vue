<script setup lang="ts">
defineProps<{
  showCondition: Boolean
  titleCondition: Boolean
}>()

const emit = defineEmits<{ click: [] }>()
</script>

<template>
  <v-list-item
    v-if="showCondition"
    class="text-center"
    :title="titleCondition ? 'Εμφάνιση λιγότερων' : 'Εμφάνιση όλων'"
    @click="emit('click')"
    variant="tonal"
  />
</template>

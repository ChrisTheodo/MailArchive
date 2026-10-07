<template>
  <div class="toolbar">
    <span class="heading">Dashboard</span>
    <v-spacer />
    <v-btn icon="i-mdi-refresh" variant="text" size="small" class="ml-2" />
  </div>
  <v-divider />
</template>

<style scoped>
.toolbar {
  display: flex;
  align-items: center;
  padding: 8px 16px;
  min-height: 52px;
}

.heading {
  font-size: 1.1rem;
  font-weight: 500;
}
</style>

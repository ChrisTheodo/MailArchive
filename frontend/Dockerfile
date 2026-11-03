# MailArchive web (Vue 3 + Vite) served by nginx — build context: ./frontend
FROM node:24-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
# Empty base URL => the SPA calls relative /api/... which nginx proxies to the API app
ENV VITE_API_BASE_URL=""
RUN npm run build-only

FROM nginxinc/nginx-unprivileged:1.29-alpine AS runtime
# API_UPSTREAM is substituted into the template at container start
ENV API_UPSTREAM=http://archive-api
COPY nginx/default.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080

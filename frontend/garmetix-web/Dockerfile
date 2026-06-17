FROM node:22-alpine AS build
WORKDIR /app

# Nuxt/Nitro production build can exceed Node's default ~2GB heap after Stage 7 dashboard pages.
# Keep this inside Docker so local npm dev workflow remains unchanged.
ENV NODE_OPTIONS=--max-old-space-size=4096
ENV NUXT_TELEMETRY_DISABLED=1

COPY frontend/garmetix-web/package*.json ./
RUN npm ci
COPY frontend/garmetix-web/ ./
RUN npm run build

FROM node:22-alpine
WORKDIR /app
ENV NODE_OPTIONS=--max-old-space-size=1024
# Nuxt UI's local icon server bundle dynamically imports the Iconify collection at runtime.
# Install it in the production image so login/dashboard icon requests do not call api.iconify.design
# and do not fail with ERR_MODULE_NOT_FOUND.
RUN npm install --no-save @iconify-json/lucide
COPY --from=build /app/.output ./.output
ENV HOST=0.0.0.0
ENV PORT=3000
EXPOSE 3000
CMD ["node", ".output/server/index.mjs"]

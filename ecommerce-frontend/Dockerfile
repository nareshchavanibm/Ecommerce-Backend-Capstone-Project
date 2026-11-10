FROM node:22-alpine AS build

WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .

ARG API_URL
ARG GOOGLE_MAPS_API_KEY=
RUN test -n "$API_URL" \
 && sed -i "s|__API_URL__|${API_URL}|; s|__GOOGLE_MAPS_API_KEY__|${GOOGLE_MAPS_API_KEY}|" src/environments/environment.production.ts

RUN npm run build -- --configuration production

FROM node:22-alpine AS final

WORKDIR /app

ENV NODE_ENV=production \
    PORT=4000

# The SSR bundle keeps express and @angular/ssr external, so production dependencies are needed at runtime.
COPY package.json package-lock.json ./
RUN npm ci --omit=dev --ignore-scripts && npm cache clean --force

COPY --from=build /app/dist/ecommerce-frontend ./dist/ecommerce-frontend

EXPOSE 4000

USER 1000

CMD ["node", "dist/ecommerce-frontend/server/server.mjs"]

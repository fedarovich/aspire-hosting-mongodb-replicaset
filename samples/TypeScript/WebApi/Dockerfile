FROM node:22-slim AS base
WORKDIR /app
EXPOSE 3000

FROM node:22-slim AS build
WORKDIR /src
COPY package.json package-lock.json* ./
RUN npm ci
COPY . .
RUN npm run build

FROM base AS final
WORKDIR /app
COPY --from=build /src/package.json /src/package-lock.json* ./
RUN npm ci --omit=dev
COPY --from=build /src/dist ./dist
ENV PORT=3000
ENV NODE_OPTIONS=--use-openssl-ca
ENTRYPOINT ["node", "dist/index.js"]

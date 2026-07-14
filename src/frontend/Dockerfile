FROM node:24-alpine AS dependencies
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund

FROM dependencies AS development
WORKDIR /app

COPY . .

EXPOSE 5173

CMD ["npm", "run", "dev"]

FROM dependencies AS build
WORKDIR /app

COPY . .
RUN npm run build

FROM nginx:1.29-alpine AS production

COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]

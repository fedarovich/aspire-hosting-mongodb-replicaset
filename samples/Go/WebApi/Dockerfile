FROM golang:1.24 AS build
WORKDIR /src
COPY *.go ./
RUN go mod init webapi \
    && go get go.mongodb.org/mongo-driver/v2/bson \
    && go get go.mongodb.org/mongo-driver/v2/mongo \
    && go get go.mongodb.org/mongo-driver/v2/mongo/options \
    && CGO_ENABLED=0 go build -o /app/webapi .

FROM gcr.io/distroless/static-debian12 AS final
WORKDIR /app
COPY --from=build /app/webapi .
ENV PORT=8080
ENTRYPOINT ["./webapi"]
